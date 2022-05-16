using BedrockWire.Core.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace BedrockWire.Core
{
    public class StreamPacketReader : IPacketReader
    {
        private Stream stream;

        public StreamPacketReader(Stream stream)
        {
            this.stream = stream;
        }

        public void Read(Action<Packet> packetCallback)
        {
            var reader = BedrockWireFormat.BedrockWireFormat.GetReader(stream);
            uint readIndex = 0;

            try
            {
                while (true)
                {
                    var packet = reader.Read();
                    packetCallback(new Packet()
                    {
                        Index = readIndex++,
                        Direction = packet.Direction == BedrockWireFormat.PacketDirection.Serverbound ? "C -> S" : "S -> C",
                        Id = packet.Id,
                        Payload = packet.Payload.ToArray(),
                        Length = (ulong)packet.Payload.Length,
                        Time = packet.Time,
                    });

                }
            }
            catch (EndOfStreamException)
            {
                return;
            }
        }
    }
}
