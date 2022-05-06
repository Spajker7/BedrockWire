using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MiNET.LevelDB.Utils;
using MiNET.Net;
using MiNET.Utils.IO;

namespace BedrockWireProxy
{
	public class RawMinecraftPacket : Packet<RawMinecraftPacket>
	{
		public ReadOnlyMemory<byte> Payload { get; private set; }

		public RawMinecraftPacket() : this(0, null)
		{
		}

		public RawMinecraftPacket(byte id, ReadOnlyMemory<byte> payload)
		{
			IsMcpe = true;
			Payload = payload;
			Id = id;
		}

		public override Packet Decode(ReadOnlyMemory<byte> buffer)
		{
			MemoryStreamReader reader = new MemoryStreamReader(buffer);
			Id = (byte) VarInt.ReadInt32(reader);
			Payload = reader.Read(reader.Length - reader.Position);

			return this;
		}

		public override byte[] Encode()
		{
			MemoryStream stream =  new MemoryStream();
			VarInt.WriteInt32(stream, Id);
			stream.Write(Payload.Span);
			stream.Flush();

			byte[] data = stream.ToArray();
			stream.Dispose();

			return data;
		}
	}
}
