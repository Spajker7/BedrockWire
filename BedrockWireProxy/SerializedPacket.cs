using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireProxy
{
    public class SerializedPacket
    {
        public PacketDirection Direction { get; set; }
        public RawMinecraftPacket Packet { get; set; }
        public ulong Time { get; set; }
    }
}
