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
        public readonly Span<byte> Lookaround;
        public int LookaroundIndex;
        public int OutIndex;
        public int CompressedIndex;
        public int BytesRead;

        public DecoderContext(Span<byte> lookaround)
        {
            Lookaround = lookaround;
        }
    }

    public static void Decode(Span<byte> compressed, Span<byte> decompressed)
    {
        Span<byte> lookaround = stackalloc byte[0x1FFF];
        var context = new DecoderContext(lookaround);
        Decode(ref context, compressed, decompressed);
    }

    private static void Decode(ref DecoderContext context, Span<byte> compressed, Span<byte> decompressed)
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
                    decompressed[context.OutIndex] = compressed[context.CompressedIndex++];

                    // Every byte that is read to the output buffer also needs to be read into
                    // the lookaround.
                    context.Lookaround[context.LookaroundIndex++] = decompressed[context.OutIndex++];
                    context.BytesRead++;

                    if (context.LookaroundIndex == context.Lookaround.Length)
                    {
                        context.LookaroundIndex %= context.Lookaround.Length;
                    }

                    break;
                case (PrsInstruction.Pointer, _, _) ptr:
                {
                    var (_, prsOffset, prsSize) = ptr;
                    ReadPointer(ref context, prsOffset, prsSize, decompressed.Slice(context.OutIndex, prsSize));
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

        // Compute the start position of the source region of the lookaround array.
        // The source region is [loadIndex, loadIndex + toRead), wrapping if necessary.
        // The destination region is [context.LookaroundIndex, context.LookaroundIndex + toRead),
        // also wrapping if necessary.
        var loadIndex = context.LookaroundIndex + offset;
        var lookaroundLength = context.Lookaround.Length;
        if (loadIndex < 0)
        {
            loadIndex += lookaroundLength;
        }

        if (loadIndex > lookaroundLength)
        {
            loadIndex %= lookaroundLength;
        }

        if (CanFastCopy(context.LookaroundIndex, lookaroundLength, loadIndex, toRead))
        {
            FastCopy(ref context, loadIndex, toRead, buffer);
        }
        else
        {
            ComplexCopy(ref context, loadIndex, toRead, buffer);
        }

        context.LookaroundIndex = (context.LookaroundIndex + toRead) % lookaroundLength;
    }

    private static void FastCopy(ref DecoderContext context, int loadIndex, int toRead, Span<byte> buffer)
    {
        var copySrc = context.Lookaround.Slice(loadIndex, toRead);
        copySrc.CopyTo(buffer);
        copySrc.CopyTo(context.Lookaround.Slice(context.LookaroundIndex, toRead));
    }

    private static void ComplexCopy(ref DecoderContext context, int loadIndex, int toRead, Span<byte> buffer)
    {
        var lookaroundLength = context.Lookaround.Length;

        // Compute the optimal safe copy ranges
        Span<int> sourceRange = stackalloc int[6];
        sourceRange[0] = loadIndex;
        sourceRange[1] = loadIndex + toRead - 1;

        Span<int> destinationRange = stackalloc int[6];
        destinationRange[0] = context.LookaroundIndex;
        destinationRange[1] = context.LookaroundIndex + toRead - 1;

        var cuts = InclusiveRangeUtils.AlignRangesOverCut(sourceRange, destinationRange, lookaroundLength);

        // Perform block copy operations according to the computed ranges
        for (var cut = 0; cut < cuts; cut++)
        {
            var sr = sourceRange.Slice(cut * 2, 2);
            var dr = destinationRange.Slice(cut * 2, 2);

            var (copySrcStart, copySrcLength) = IntervalToRange(sr[0], sr[1]);
            var (copyDstStart, copyDstLength) = IntervalToRange(dr[0], dr[1]);

            var copySrcStartWrapped = copySrcStart % lookaroundLength;
            var copySrc = context.Lookaround.Slice(copySrcStartWrapped, copySrcLength);

            var copyDstBufferWrapped = (copyDstStart - context.LookaroundIndex) % lookaroundLength;
            var copyDstBuffer = buffer.Slice(copyDstBufferWrapped, copyDstLength);

            var copyDstLookaroundWrapped = copyDstStart % lookaroundLength;
            var copyDstLookaround = context.Lookaround.Slice(copyDstLookaroundWrapped, copyDstLength);

            copySrc.CopyTo(copyDstBuffer);
            copySrc.CopyTo(copyDstLookaround);
        }
    }

    private static bool CanFastCopy(int lookaroundIndex, int lookaroundLength, int loadIndex, int size)
    {
        return Math.Max(lookaroundIndex, loadIndex) + size <= lookaroundLength &&
               Math.Abs(lookaroundIndex - loadIndex) >= size;
    }

    private static (int, int) IntervalToRange(int start, int end)
    {
        return (start, end - start + 1);
    }

    private static (PrsInstruction, int, int) GetNextInstruction(Span<byte> compressed, ref DecoderContext context)
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

    private static (int, int) ReadLongRun(Span<byte> compressed, ref DecoderContext context, int data0, int data1)
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

    private static (int, int) ReadShortRun(Span<byte> compressed, ref DecoderContext context)
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

    private static bool GetControlBit(Span<byte> compressed, ref DecoderContext context)
    {
        if (context.ControlCounter == 8)
        {
            context.Control.Data = compressed[context.CompressedIndex++];
            context.ControlCounter = 0;
        }

        return context.Control.GetBit(context.ControlCounter++);
    }
}