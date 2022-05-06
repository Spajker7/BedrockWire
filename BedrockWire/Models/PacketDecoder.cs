using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public class PacketDecoder
    {
        public List<PacketField>? Fields { get; set; }

        public (Dictionary<object, object>? Result, string? Error) Decode(BinaryReader reader)
        {
            Dictionary<object, object> packet = new Dictionary<object, object>();

            foreach (PacketField field in Fields)
            {
                if (FieldDecoders.Decoders.ContainsKey(field.Type))
                {
                    object value = FieldDecoders.Decoders[field.Type](reader);
                    packet[field.Name] = value;

                }
                else if (FieldDecoders.ComplexDecoders.ContainsKey(field.Type))
                {
                    object value = FieldDecoders.ComplexDecoders[field.Type](reader, field.SubFields);
                    packet[field.Name] = value;
                }
                else if (FieldDecoders.CustomDecoders.ContainsKey(field.Type))
                {
                    object value = FieldDecoders.CustomDecoders[field.Type].Decode(reader).Result;
                    packet[field.Name] = value;
                }
                else
                {
                    return (null, "Unknown type: " + field.Type);
                }

                var key = field.Name;

                if (!string.IsNullOrEmpty(field.Conditional))
                {
                    string condition = field.Conditional;
                    bool isNegative = condition[0] == '!';
                    if (isNegative)
                    {
                        condition = condition.Substring(1);
                    }
                    var value = packet[field.Name].ToString();

                    if ((!isNegative && condition.Equals(value)) || (isNegative && !condition.Equals(value)))
                    {
                        PacketDecoder packetDecoder = new PacketDecoder() { Fields = field.SubFields };
                        packet[field.Name + ": " + value] = packetDecoder.Decode(reader).Result;
                        key = field.Name + ": " + value;
                        packet.Remove(field.Name);
                    }
                }
                else if (field.IsSwitch)
                {
                    var value = packet[field.Name].ToString();
                    PacketField? elm = field.SubFields.Where(x => x.Case.Equals(value)).LastOrDefault();

                    if (elm != null)
                    {
                        PacketDecoder packetDecoder = new PacketDecoder() { Fields = new List<PacketField>() { elm } };
                        packet[field.Name + ": " + value] = packetDecoder.Decode(reader).Result;
                        key = field.Name + ": " + value;
                        packet.Remove(field.Name);
                    }
                }

                if (field.IgnoreContainer)
                {
                    if (packet[key] is Dictionary<object, object> dict)
                    {
                        packet.Remove(key);
                        foreach (object key2 in dict.Keys)
                        {
                            packet[key2] = dict[key2];
                        }
                    }
                }

            }

            return (packet, null);
        }
    }
}
