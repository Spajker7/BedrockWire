using BedrockWire.Core.Model;
using System;

namespace BedrockWire.Core
{
    public interface IPacketReader
    {
        public void Read(Action<Packet> packetCallback);
    }
}
