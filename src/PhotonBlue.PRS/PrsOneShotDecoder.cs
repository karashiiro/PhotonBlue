using System.Diagnostics;

namespace PhotonBlue.PRS;

/// <summary>
/// A PRS decoder that decodes an entire compressed buffer in one pass.
/// </summary>
public class PrsOneShotDecoder
{
    private const int MinLongCopyLength = 10;

    // Making this a long seems to significantly improve decompression
    // speeds. I suspect that this is because GetNextInstruction returns
    // a tuple of (PrsInstruction, int, int), and making this into an
    // 8-byte value allows that struct to be naturally memory-aligned.
    private enum PrsInstruction : long
    {
        Eof,
        Literal,
        Pointer,
    }

    private ref struct ControlByte
    {
        public byte Data;

        public ControlByte(byte data)
        {
            Data = data;
        }

        public readonly bool GetBit(int offset)
        {
            return (Data & (1 << offset)) > 0;
        }
    }

    private ref struct DecoderContext
    {
        public ControlByte Control = new(0);
        public int ControlCounter = 8;
        public int OutIndex;
        public int CompressedIndex;
        public int BytesRead;

        public DecoderContext()
        {
        }
    }

    public static void Decode(Span<byte> compressed, Span<byte> decompressed)
    {
        // This does not actually require a lookaround buffer if
        // everything is decoded in a single pass.
        var context = new DecoderContext();
        Decode(ref context, compressed, decompressed);
    }

    private static void Decode(ref DecoderContext context, ReadOnlySpan<byte> compressed, Span<byte> decompressed)
    {
        while (context.OutIndex < decompressed.Length)
        {
            var inst = GetNextInstruction(compressed, ref context);
            switch (inst)
            {
                case (PrsInstruction.Eof, _, _):
                    return;
                case (PrsInstruction.Literal, _, _):
                    // Raw data read
                    decompressed[context.OutIndex++] = compressed[context.CompressedIndex++];
                    context.BytesRead++;
                    break;
                case (PrsInstruction.Pointer, _, _) ptr:
                {
                    var (_, prsOffset, prsSize) = ptr;
                    ReadPointer(ref context, prsOffset, prsSize, decompressed);
                    context.OutIndex += prsSize;
                    break;
                }
            }
        }
    }

    private static void ReadPointer(ref DecoderContext context, int offset, int toRead, Span<byte> buffer)
    {
        Debug.Assert(offset != 0 && context.BytesRead >= -offset, "Bad copy instruction detected.");
        context.BytesRead += toRead;

        var loadIndex = context.OutIndex + offset;
        if (CanFastCopy(context.OutIndex, loadIndex, toRead))
        {
            FastCopy(ref context, loadIndex, toRead, buffer);
        }
        else
        {
            ComplexCopy(ref context, loadIndex, toRead, buffer);
        }
    }

    private static void FastCopy(ref DecoderContext context, int loadIndex, int toRead, Span<byte> buffer)
    {
        var copySrc = buffer.Slice(loadIndex, toRead);
        copySrc.CopyTo(buffer.Slice(context.OutIndex, toRead));
    }

    private static void ComplexCopy(ref DecoderContext context, int loadIndex, int toRead, Span<byte> buffer)
    {
        // Compute the optimal safe copy ranges
        Span<int> sourceRange = stackalloc int[6];
        sourceRange[0] = loadIndex;
        sourceRange[1] = loadIndex + toRead - 1;

        Span<int> destinationRange = stackalloc int[6];
        destinationRange[0] = context.OutIndex;
        destinationRange[1] = context.OutIndex + toRead - 1;

        var cuts = InclusiveRangeUtils.AlignRangesOverCut(sourceRange, destinationRange);

        // Perform block copy operations according to the computed ranges
        for (var cut = 0; cut < cuts; cut++)
        {
            var sr = sourceRange.Slice(cut * 2, 2);
            var dr = destinationRange.Slice(cut * 2, 2);

            var (copySrcStart, copySrcLength) = IntervalToRange(sr[0], sr[1]);
            var (copyDstStart, copyDstLength) = IntervalToRange(dr[0], dr[1]);

            var copySrc = buffer.Slice(copySrcStart, copySrcLength);
            var copyDst = buffer.Slice(copyDstStart, copyDstLength);

            copySrc.CopyTo(copyDst);
        }
    }

    private static bool CanFastCopy(int outIndex, int loadIndex, int size)
    {
        return Math.Abs(outIndex - loadIndex) >= size;
    }

    private static (int, int) IntervalToRange(int start, int end)
    {
        return (start, end - start + 1);
    }

    private static (PrsInstruction, int, int) GetNextInstruction(ReadOnlySpan<byte> compressed, ref DecoderContext context)
    {
        if (GetControlBit(compressed, ref context))
        {
            return (PrsInstruction.Literal, 0, 0);
        }

        if (GetControlBit(compressed, ref context))
        {
            var data0 = compressed[context.CompressedIndex++];
            var data1 = compressed[context.CompressedIndex++];
            if (ReadEof(data0, data1))
            {
                return (PrsInstruction.Eof, 0, 0);
            }

            // Long search; long size or long search; short size
            var (offset, size) = ReadLongRun(compressed, ref context, data0, data1);
            return (PrsInstruction.Pointer, offset, size);
        }
        else
        {
            // Short search; short size
            var (offset, size) = ReadShortRun(compressed, ref context);
            return (PrsInstruction.Pointer, offset, size);
        }
    }

    private static bool ReadEof(int data0, int data1)
    {
        return data0 == 0 && data1 == 0;
    }

    private static (int, int) ReadLongRun(ReadOnlySpan<byte> compressed, ref DecoderContext context, int data0, int data1)
    {
        var controlOffset = (data1 << 5) + (data0 >> 3) - 8192;
        var controlSize = data0 & 0b00000111;

        if (controlSize == 0)
        {
            // Long search; long size
            controlSize = compressed[context.CompressedIndex++] + MinLongCopyLength;
        }
        else
        {
            // Long search; short size
            controlSize += 2;
        }

        return (controlOffset, controlSize);
    }

    private static (int, int) ReadShortRun(ReadOnlySpan<byte> compressed, ref DecoderContext context)
    {
        var controlSize = 2;
        if (GetControlBit(compressed, ref context))
        {
            controlSize += 2;
        }

        if (GetControlBit(compressed, ref context))
        {
            controlSize++;
        }

        var controlOffset = compressed[context.CompressedIndex++] - 256;

        return (controlOffset, controlSize);
    }

    private static bool GetControlBit(ReadOnlySpan<byte> compressed, ref DecoderContext context)
    {
        if (context.ControlCounter == 8)
        {
            context.Control.Data = compressed[context.CompressedIndex++];
            context.ControlCounter = 0;
        }

        return context.Control.GetBit(context.ControlCounter++);
    }
}