using BedrockWire.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public class PacketField
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }

    // TODO: move this elsewhere
    public class PacketDecoder
    {
        public List<PacketField> Fields { get; set; }

        public Dictionary<object, object>? Decode(byte[] payload)
        {
            using(MemoryStream stream = new MemoryStream(payload))
            {
                using(BinaryReader reader = new BinaryReader(stream))
                {
                    Dictionary<object, object> packet = new Dictionary<object, object>();

                    foreach (PacketField field in Fields)
                    {
                        if(!FieldDecoders.Decoders.ContainsKey(field.Type))
                        {
                            return null; // no decoder for field, error out
                        }

                        object value = FieldDecoders.Decoders[field.Type](reader);
                        packet[field.Name] = value;
                    }

                    if(stream.Position != stream.Length)
                    {
                        return null;
                    }

                    return packet;
                }
            }
        }
    }

    public class PacketDefinition
    {
        public string Name { get; set; }
        public PacketDecoder PacketDecoder { get; set; }
    }
}
