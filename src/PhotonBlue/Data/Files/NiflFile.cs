using System.Diagnostics;
using System.Text;
using PhotonBlue.Extensions;
// ReSharper disable NotAccessedField.Global

namespace PhotonBlue.Data.Files;

public class NiflFile : FileResource
{
    public struct FileHeader
    {
        public uint Magic; // NIFL
        public uint Reserved1;
        public uint Reserved2;
        public int Rel0Offset;
        public int Rel0Size;
        public int Nof0Offset;
        public int Nof0Size;
        public uint Reserved3;

        public static FileHeader Read(BinaryReader reader)
        {
            return new()
            {
                Magic = reader.ReadUInt32(),
                Reserved1 = reader.ReadUInt32(),
                Reserved2 = reader.ReadUInt32(),
                Rel0Offset = reader.ReadInt32(),
                Rel0Size = reader.ReadInt32(),
                Nof0Offset = reader.ReadInt32(),
                Nof0Size = reader.ReadInt32(),
                Reserved3 = reader.ReadUInt32(),
            };
        }
    }

    public struct Rel0Header
    {
        public uint Magic; // REL0
        public int Size;
        public int EntrySize;
        public uint Reserved;

        public static Rel0Header Read(BinaryReader reader)
        {
            return new()
            {
                Magic = reader.ReadUInt32(),
                Size = reader.ReadInt32(),
                EntrySize = reader.ReadInt32(),
                Reserved = reader.ReadUInt32(),
            };
        }
    }

    public struct Nof0Header
    {
        public uint Magic; // NOF0
        public int Size;
        public int Count;

        public static Nof0Header Read(BinaryReader reader)
        {
            return new()
            {
                Magic = reader.ReadUInt32(),
                Size = reader.ReadInt32(),
                Count = reader.ReadInt32(),
            };
        }
    }

    public FileHeader Header { get; private set; }
    public Rel0Header Rel0 { get; private set; }
    public Nof0Header Nof0 { get; private set; }

    public NiflFile(Stream data) : base(data, null!)
    {
    }

    public override void LoadFile()
    {
        Debug.Assert(Reader != null);
        
        Header = FileHeader.Read(Reader);
        Reader.Seek(Header.Rel0Offset, SeekOrigin.Begin);
        Rel0 = Rel0Header.Read(Reader);
        Reader.Seek(Header.Nof0Offset, SeekOrigin.Begin);
        Nof0 = Nof0Header.Read(Reader);
    }

    public IList<string> ReadText()
    {
        Debug.Assert(Reader != null);
        
        Reader.Seek(Header.Rel0Offset + 16, SeekOrigin.Begin);
        var controls = new uint[Nof0.Count];
        for (var i = 0; i < Nof0.Count; i++)
        {
            controls[i] = Reader.ReadUInt32();
        }

        var text = new List<string>();
        var pairMode = false;
        var even = -1;

        // FF FF FF FF is pair mode; data swaps between UTF8 and UTF16
        if (controls[0] == 0xffffffffU)
        {
            pairMode = true;
            even = 0;
        }

        for (var i = 1; i < controls.Length; i++)
        {
            if (controls[i] == 0xffffffffU)
            {
                pairMode = true;
                even = 0;
            }
            else if (controls[i] == 0x00000014U)
            {
                break;
            }
            else
            {
                // The reference implementation in NIFLnew uses a loop here
                // corresponding to the count in NOF0, but doing that seems to
                // fail on tut_006353.text.
                Reader.Seek(Header.Rel0Offset + controls[i], SeekOrigin.Begin);

                // Take bytes until encountering a null-terminator for the
                // current encoding
                var str = new List<byte>();
                do
                {
                    str.Add(Reader.ReadByte());
                } while (str.Count < 2 || (even == 1 && str[^2] != 0) || str[^1] != 0);

                var encoding = even == 1 ? Encoding.Unicode : Encoding.UTF8;
                text.Add(encoding.GetString(str.ToArray()));

                if (pairMode)
                {
                    even ^= 1;
                }
            }
        }

        return text;
    }
}