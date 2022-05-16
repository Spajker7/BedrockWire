namespace BedrockWire.Core.Model
{
    public class Packet
    {
        public uint Index { get; set; }
        public string Direction { get; set; }
        public int Id { get; set; }
        public ulong Time { get; set; }
        public ulong Length { get; set; }
        public byte[] Payload { get; set; }

        public string Name { get; set; }
        public Dictionary<object, object>? Decoded { get; set; }
        public string? Error { get; set; }
    }
}
