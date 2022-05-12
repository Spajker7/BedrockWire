using BedrockWireFormat.Versions;

namespace BedrockWireFormat
{
    public static class BedrockWireFormat
    {
        private static readonly byte[] FORMAT_HEADER = new byte[] { 66, 68, 87 };
        private const byte LATEST_FORMAT_VERSION = 1;

        public static PacketReader GetReader(Stream stream)
        {
            return GetReader(ReadHeader(stream), stream);
        }

        public static PacketWriter GetWriter(Stream stream)
        {
            return GetWriter(LATEST_FORMAT_VERSION, stream);
        }

        public static PacketReader GetReader(byte version, Stream stream)
        {
            switch(version)
            {
                case 1:
                    return new Version1Reader(stream);
                default:
                    throw new ArgumentException("Unknown format version: " + version);
            }
        }

        public static PacketWriter GetWriter(byte version, Stream stream)
        {
            switch (version)
            {
                case 1:
                    return new Version1Writer(stream);
                default:
                    throw new ArgumentException("Unknown format version: " + version);
            }
        }

        public static byte ReadHeader(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            byte[] header = new byte[FORMAT_HEADER.Length + 1];
            reader.Read(header, 0, FORMAT_HEADER.Length + 1);

            for(int i = 0; i < FORMAT_HEADER.Length; i++)
            {
                if(header[i] != FORMAT_HEADER[i])
                {
                    throw new Exception("Invalid header!");
                }
            }

            return header[FORMAT_HEADER.Length]; // version
        }

        public static void WriteHeader(byte version, Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(FORMAT_HEADER);
            writer.Write(version);
        }

        public static void WriteHeader(Stream stream)
        {
            WriteHeader(LATEST_FORMAT_VERSION, stream);
        }
    }
}