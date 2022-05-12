using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireFormat
{

    public abstract class PacketReader
    {
        public abstract Packet Read();
    }
}
