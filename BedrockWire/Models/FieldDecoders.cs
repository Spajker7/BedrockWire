using BedrockWire.Utils;
using Jose;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public class FieldDecoders
    {
        private static object ReadIntBe(BinaryReader reader)
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadInt32());
        }

        private static object ReadByte(BinaryReader reader)
        {
            return reader.ReadByte();
        }

        private static object ReadBool(BinaryReader reader)
        {
            return reader.ReadByte() != 0;
        }

        private static object ReadLength(BinaryReader reader)
        {
            return (int)VarInt.ReadUInt32(reader);
        }

        private static object ReadUnsignedVarInt(BinaryReader reader)
        {
            return VarInt.ReadUInt32(reader);
        }

        private static object ReadString(BinaryReader reader)
        {
            if (reader.BaseStream.Position == reader.BaseStream.Length) return string.Empty;
            int len = (int)ReadLength(reader);
            if (len <= 0) return string.Empty;
            return Encoding.UTF8.GetString(reader.ReadBytes(len));
        }

        private static object ReadString32(BinaryReader reader)
        {
            if (reader.BaseStream.Position == reader.BaseStream.Length) return string.Empty;
            int len = reader.ReadInt32();
            if (len <= 0) return string.Empty;
            return Encoding.UTF8.GetString(reader.ReadBytes(len));
        }

        private static object ReadByteArray(BinaryReader reader)
        {
            if (reader.BaseStream.Position == reader.BaseStream.Length) return new byte[0];
            int len = (int)ReadLength(reader);
            if (len <= 0) return new byte[0];
            return reader.ReadBytes(len);
        }

        private static object ReadJson(BinaryReader reader)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>((string)ReadString(reader));
        }

        private static object ReadJsonJwtArrayChain(BinaryReader reader)
        {
            string a = (string)ReadString32(reader);

            var json = JsonConvert.DeserializeObject<Dictionary<object, object>>(a);

            var list = json["chain"] as JArray;
            Dictionary<object, object> data = new Dictionary<object, object>();

            for(int i = 0; i < list.Count; i++)
            {
                data[i] = JsonConvert.DeserializeObject<Dictionary<object, object>>(JWT.Payload(list[i].ToString()));
            }

            return data;
        }

        private static object ReadJwt(BinaryReader reader)
        {
            return JsonConvert.DeserializeObject<Dictionary<object, object>>(JWT.Payload((string)ReadString32(reader)));
        }

        public static readonly Dictionary<string, Func<BinaryReader, object>> Decoders = new Dictionary<string, Func<BinaryReader, object>>()
        {
            {"intBE", ReadIntBe},
            {"bool", ReadBool},
            {"string", ReadString},
            {"byteArray", ReadByteArray },
            {"byte", ReadByte },
            {"uvarint", ReadUnsignedVarInt },
            {"json", ReadJson },
            {"jsonJwtArrayChain", ReadJsonJwtArrayChain },
            {"jwt", ReadJwt }
        };
    }
}
