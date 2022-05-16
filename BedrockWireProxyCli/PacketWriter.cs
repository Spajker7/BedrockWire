using BedrockWireFormat;
using BedrockWireProxy;
using BedrockWireProxyInterface;
using System.Collections.Concurrent;

namespace BedrockWireProxyCli
{
    public class PacketWriter : IPacketWriter
	{
		private BedrockWireFormat.PacketWriter Writer { get;}
		private string Format { get; }
        public BlockingCollection<Packet> Queue { get; set; }

        public PacketWriter(string output, string format)
		{
			Queue = new BlockingCollection<Packet>(10000);

			Stream underlyingStream;

			if("stdout".Equals(output, StringComparison.OrdinalIgnoreCase))
			{
				underlyingStream = Console.OpenStandardOutput();
			}
			else
			{
				underlyingStream = new FileStream(output, FileMode.Create);
			}

			BedrockWireFormat.BedrockWireFormat.WriteHeader(underlyingStream);
			Writer = BedrockWireFormat.BedrockWireFormat.GetWriter(underlyingStream);
			Format = format;
		}

		public void StartWriting(CancellationToken cancellationToken)
        {
			while(true)
            {
				try
                {
					var elm = Queue.Take(cancellationToken);
					Writer.Write(elm);
				}
				catch(OperationCanceledException)
                {
					break;
				}
            }
        }

        public void WritePacket(PacketDirection direction, byte id, ReadOnlyMemory<byte> payload, ulong time)
		{
			Queue.Add(new Packet() { Id = id, Payload = payload, Length = (uint) payload.Length, Time = time , Direction = direction });
		}
    }
}
