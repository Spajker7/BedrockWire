using System.IO.Compression;

namespace BedrockWireFormat.Versions
{
    public class Version1Reader : PacketReader
    {
        private BinaryReader Reader { get; set; }

        public Version1Reader(Stream stream)
        {
            Reader = new BinaryReader(new DeflateStream(stream, CompressionMode.Decompress));
        }

        public override Packet Read()
        {
            byte direction = Reader.ReadByte();
            byte packetId = Reader.ReadByte();
            ulong time = Reader.ReadUInt64();
            uint length = Reader.ReadUInt32();
            byte[] payload = Reader.ReadBytes((int) length);

            return new Packet() { Direction = (PacketDirection) direction, Id = packetId, Payload = payload, Time = time, Length = length };
        }
    }

    public class Version1Writer : PacketWriter
    {
        private BinaryWriter Writer { get; set; }

        public Version1Writer(Stream stream)
        {
            Writer = new BinaryWriter(new DeflateStream(stream, CompressionMode.Compress));
        }

        public override void Write(Packet packet)
        {
            Writer.Write((byte)packet.Direction);
            Writer.Write(packet.Id);
            Writer.Write(packet.Time);
            Writer.Write(packet.Length);
            Writer.Write(packet.Payload.ToArray());
            Writer.Flush();
        }
    }
}
