using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireFormat
{
    public class Packet
    {
        public PacketDirection Direction { get; set; }
        public byte Id { get; set; }
        public uint Length { get; set; }
        public ReadOnlyMemory<byte> Payload { get; set; }
        public ulong Time { get; set; }
    }
}
