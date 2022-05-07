using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireProxy
{
	public class PacketWriter
	{
		private BinaryWriter OutputWriter { get;}
		private string Format { get; }
        public BlockingCollection<SerializedPacket> Queue { get; set; }

        public PacketWriter(string output, string format)
		{
			Queue = new BlockingCollection<SerializedPacket>(10000);

			Stream underlyingStream;

			if("stdout".Equals(output, StringComparison.OrdinalIgnoreCase))
			{
				underlyingStream = Console.OpenStandardOutput();
			}
			else
			{
				underlyingStream = new FileStream(output, FileMode.Create);
			}

			byte[] header = { 66, 68, 87 };
			underlyingStream.Write(header);
			underlyingStream.WriteByte(1); // version

			OutputWriter = new BinaryWriter(new DeflateStream(underlyingStream, CompressionMode.Compress));
			Format = format;
		}

		public void StartWriting(CancellationToken cancellationToken)
        {
			long lastTimeMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

			while(true)
            {
				try
                {
					var elm = Queue.Take(cancellationToken);
					OutputWriter.Write((byte) elm.Direction);
					OutputWriter.Write(elm.Packet.Id);
					OutputWriter.Write(elm.Time);
					OutputWriter.Write(elm.Packet.Payload.Length);
					OutputWriter.Write(elm.Packet.Payload.ToArray());

				}
				catch(OperationCanceledException)
                {
					break;
				}

				long now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

				if(now - 5000 > lastTimeMs)
                {
					lastTimeMs = now;
					Console.Clear();
					Console.WriteLine("Packets in queue: " + Queue.Count);
				}
            }
        }

        public void WritePacket(PacketDirection direction, RawMinecraftPacket packet, ulong time)
		{
			Queue.Add(new SerializedPacket() { Packet = packet, Time = time , Direction = direction });
		}
	}
}
