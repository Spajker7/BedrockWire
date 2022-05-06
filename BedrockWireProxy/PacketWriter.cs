using System;
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

		public PacketWriter(string output, string format)
		{
			if("stdout".Equals(output, StringComparison.OrdinalIgnoreCase))
			{
				OutputWriter = new BinaryWriter(Console.OpenStandardOutput());
			}
			else
			{
				OutputWriter = new BinaryWriter(new DeflateStream(new FileStream(output, FileMode.Create), CompressionMode.Compress));
				//OutputWriter = new BinaryWriter(new FileStream(output, FileMode.Create));
			}

			Format = format;
		}

		public void WritePacket(PacketDirection direction, RawMinecraftPacket packet, ulong time)
		{
			lock(this)
			{
				OutputWriter.Write((byte) direction);
				OutputWriter.Write(packet.Id);
				OutputWriter.Write(time);
				OutputWriter.Write(packet.Payload.Length);
				OutputWriter.Write(packet.Payload.ToArray());
			}
		}
	}
}
