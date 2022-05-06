using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public class Packet
    {
        public string Direction { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public ulong Time { get; set; }
        public ulong Length { get; set; }
        public byte[] Payload { get; set; }
        public Dictionary<object, object> Decoded { get; set; }
        public bool HasError { get; set; }
    }
}
