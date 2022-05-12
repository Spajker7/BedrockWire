using BedrockWireFormat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireProxy
{
	public interface IPacketWriter
	{
		public void WritePacket(PacketDirection direction, RawMinecraftPacket packet, ulong time);
	}
}
