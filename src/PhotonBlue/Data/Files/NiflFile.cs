namespace PhotonBlue.Data.Files;

public class NiflFile : FileResource
{
    public struct FileHeader
    {
        public uint Magic; // NIFL
        public uint Reserved1;
        public uint Reserved2;
        public uint Rel0Offset;
        public uint Rel0Size;
        public uint Nof0Offset;
        public uint Nof0Size;
        public uint Reserved3;

        public static FileHeader Read(BinaryReader reader)
        {
            return new()
            {
                Magic = reader.ReadUInt32(),
                Reserved1 = reader.ReadUInt32(),
                Reserved2 = reader.ReadUInt32(),
                Rel0Offset = reader.ReadUInt32(),
                Rel0Size = reader.ReadUInt32(),
                Nof0Offset = reader.ReadUInt32(),
                Nof0Size = reader.ReadUInt32(),
                Reserved3 = reader.ReadUInt32(),
            };
        }
    }
    
    public struct Rel0Header
    {
        public uint Magic; // REL0
        public uint Size;
        public uint EntrySize;
        public uint Reserved;
        
        public static Rel0Header Read(BinaryReader reader)
        {
            return new()
            {
                Magic = reader.ReadUInt32(),
                Size = reader.ReadUInt32(),
                EntrySize = reader.ReadUInt32(),
                Reserved = reader.ReadUInt32(),
            };
        }
    }
    
    public struct Nof0Header
    {
        public uint Magic; // NOF0
        public uint Size;
        public uint Count;
        
        public static Nof0Header Read(BinaryReader reader)
        {
            return new()
            {
                Magic = reader.ReadUInt32(),
                Size = reader.ReadUInt32(),
                Count = reader.ReadUInt32(),
            };
        }
    }
    
    public FileHeader Header { get; private set; }
    public Rel0Header Rel0 { get; private set; }
    public Nof0Header Nof0 { get; private set; }

    public NiflFile(Stream data) : base(data)
    {
    }

    protected override void LoadFile()
    {
        Header = FileHeader.Read(Reader);
        Rel0 = Rel0Header.Read(Reader);
        Nof0 = Nof0Header.Read(Reader);
    }
}