using fNbt;
using Jose;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BedrockWire.Core
{
    public class FieldDecoders
    {
        private static object ReadIntBe(BinaryReader reader)
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadInt32());
        }

        private static object ReadInt(BinaryReader reader)
        {
            return reader.ReadInt32();
        }
        
        private static object ReadUInt(BinaryReader reader)
        {
            return reader.ReadUInt32();
        }

        private static object ReadShortBe(BinaryReader reader)
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadInt16());
        }

        private static object ReadShort(BinaryReader reader)
        {
            return reader.ReadInt16();
        }

        private static object ReadUShort(BinaryReader reader)
        {
            return reader.ReadUInt16();
        }

        private static object ReadLong(BinaryReader reader)
        {
            return reader.ReadInt64();
        }

        private static object ReadLongBE(BinaryReader reader)
        {
            return BinaryPrimitives.ReverseEndianness(reader.ReadInt64());
        }

        private static object ReadFloat(BinaryReader reader)
        {
            return reader.ReadSingle();
        }

        private static object ReadRotationFloat(BinaryReader reader)
        {
            return reader.ReadByte() * (360.0 / 256);
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

        private static object ReadDoubleVarIntProduct(BinaryReader reader)
        {
            return VarInt.ReadSInt32(reader) * VarInt.ReadSInt32(reader);
        }

        private static object ReadVarInt(BinaryReader reader)
        {
            return VarInt.ReadSInt32(reader);
        }

        private static object ReadUnsignedVarLong(BinaryReader reader)
        {
            return VarInt.ReadUInt64(reader);
        }

        private static object ReadVarLong(BinaryReader reader)
        {
            return VarInt.ReadSInt64(reader);
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
            
            if (reader.BaseStream.Position == reader.BaseStream.Length) return "";
            int len = (int)ReadLength(reader);
            if (len <= 0) return "";
            /*
            return BitConverter.ToString(reader.ReadBytes(len)).Replace("-", " ");
            */
            return reader.ReadBytes(len);
        }

        private static object ReadFixed256(BinaryReader reader)
        {
            return BitConverter.ToString(reader.ReadBytes(256)).Replace("-", " ");
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

        private static object ReadUUID(BinaryReader reader)
        {
            byte[] uuid = reader.ReadBytes(16);
            var _a = BitConverter.ToUInt64(uuid.Skip(0).Take(8).Reverse().ToArray(), 0);
            var _b = BitConverter.ToUInt64(uuid.Skip(8).Take(8).Reverse().ToArray(), 0);
            var bytes = BitConverter.GetBytes(_a).Concat(BitConverter.GetBytes(_b)).ToArray();
            string hex = string.Join("", bytes.Select(b => b.ToString("x2")));
            return hex.Substring(0, 8) + "-" + hex.Substring(8, 4) + "-" + hex.Substring(12, 4) + "-" + hex.Substring(16, 4) + "-" + hex.Substring(20, 12);
        }

        private static object ReadNBT(BinaryReader reader)
        {
            NbtFile nbtFile = new NbtFile();
            nbtFile.BigEndian = false;
            nbtFile.UseVarInt = true;
            nbtFile.AllowAlternativeRootTag = true;

            nbtFile.LoadFromStream(reader.BaseStream, NbtCompression.None);
            return nbtFile.RootTag.ToString();
        }

        public static readonly Dictionary<string, Func<BinaryReader, object>> Decoders = new Dictionary<string, Func<BinaryReader, object>>()
        {
            {"shortBE", ReadShortBe},
            {"short", ReadShort},
            {"unsignedShort", ReadUShort},
            {"intBE", ReadIntBe},
            {"int", ReadInt},
            {"unsignedInt", ReadUInt},
            {"longBE", ReadLongBE},
            {"long", ReadLong},
            {"float", ReadFloat},
            {"rotationFloat", ReadRotationFloat},
            {"bool", ReadBool},
            {"string", ReadString},
            {"byte", ReadByte },
            {"unsignedVarInt", ReadUnsignedVarInt },
            {"varInt", ReadVarInt },
            {"unsignedVarLong", ReadUnsignedVarLong },
            {"varLong", ReadVarLong },
            {"json", ReadJson },
            {"jsonJwtArrayChain", ReadJsonJwtArrayChain },
            {"jwt", ReadJwt },
            {"uuid", ReadUUID },
            {"nbt", ReadNBT },
            {"byteArray", ReadByteArray },
            {"doubleVarIntProduct", ReadDoubleVarIntProduct }, // TODO: Get rid of this
            {"fixed256", ReadFixed256 },// TODO: Get rid of this
        };
    }
}
