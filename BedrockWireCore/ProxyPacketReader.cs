using BedrockWire.Core.Model;
using BedrockWireProxyInterface;
using System;
using System.Collections.Concurrent;

namespace BedrockWire.Core
{
    public class ProxyPacketReader : IPacketReader, IPacketWriter
    {
        private BlockingCollection<Packet> packetReadQueue = new BlockingCollection<Packet>();

        public void Read(Action<Packet> packetCallback)
        {
            uint readIndex = 0;

            try
            {
                while (true)
                {
                    var packet = packetReadQueue.Take();
                    packet.Index = readIndex++;
                    packetCallback(packet);
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }
        }

        public void StopWriting()
        {
            packetReadQueue.CompleteAdding();
        }

        public void WritePacket(BedrockWireFormat.PacketDirection direction, byte id, ReadOnlyMemory<byte> payload, ulong time)
        {
            packetReadQueue.Add(new Packet() { Direction = direction == BedrockWireFormat.PacketDirection.Serverbound ? "C -> S" : "S -> C", Id = id, Payload = payload.ToArray(), Time = time, Length = (ulong) payload.Length });
        }
    }
}
