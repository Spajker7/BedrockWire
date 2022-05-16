using BedrockWireFormat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireProxyInterface
{
	public interface IPacketWriter
	{
		public void WritePacket(PacketDirection direction, byte id, ReadOnlyMemory<byte> payload, ulong time);
	}
}
