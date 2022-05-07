using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public static class PacketDecoder
    {
        public static Dictionary<object, object> Decode(BinaryReader reader, List<PacketField> packetFields)
        {
            Dictionary<object, object> packet = new Dictionary<object, object>();
            Dictionary<string, object> referenceValues = new Dictionary<string, object>();
            string previousName = null;
            object previousValue = null;

            foreach (PacketField field in packetFields)
            {
                string newName = field.Name;

                if (!string.IsNullOrEmpty(field.Conditional)) // if field
                {
                    string condition = field.Conditional;
                    bool isNegative = condition[0] == '!';
                    if (isNegative)
                    {
                        condition = condition.Substring(1);
                    }

                    string value = (field.ReferencesId != null ? referenceValues[field.ReferencesId] : previousValue).ToString();

                    if ((!isNegative && condition.Equals(value)) || (isNegative && !condition.Equals(value)))
                    {
                        packet[previousName + ": " + value] = Decode(reader, field.SubFields);
                        newName = previousName + ": " + value;
                        packet.Remove(previousName);
                    }
                }
                else if (field.IsSwitch)
                {
                    string value = (field.ReferencesId != null ? referenceValues[field.ReferencesId] : previousValue).ToString();

                    PacketField? elm = field.SubFields.Where(x => x.Case.Equals(value)).LastOrDefault();

                    if (elm != null)
                    {
                        packet[previousName + ": " + value] = Decode(reader, elm.SubFields);
                        newName = previousName + ": " + value;
                        packet.Remove(previousName);
                    }
                }
                else if (field.IsFlags)
                {
                    string strVal = (field.ReferencesId != null ? referenceValues[field.ReferencesId] : previousValue).ToString();
                    int value;
                    if (!int.TryParse(strVal, out value))
                    {
                        throw new Exception("Flags field referenced a non-integer: " + strVal);
                    }

                    Dictionary<object, object> list = new Dictionary<object, object>();
                    int i = 0;

                    foreach (PacketField x in field.SubFields)
                    {
                        int bitflag;

                        if (x.Case.StartsWith("0x"))
                        {
                            try
                            {
                                bitflag = Convert.ToInt32(x.Case, 16);
                            }
                            catch (FormatException)
                            {
                                throw new Exception("Bit flag field has bad hex: " + x.Case);
                            }
                        }
                        else if (!int.TryParse(x.Case, out bitflag))
                        {
                            throw new Exception("Bit flag field has bad number: " + x.Case);
                        }

                        if((value & bitflag) != 0)
                        {

                            list.Add(i, Decode(reader, x.SubFields));
                            i++;
                        }
                    }

                    packet[previousName + ": " + strVal] = list;
                    newName = previousName + ": " + value;
                    packet.Remove(previousName);
                }
                else if (field.IsList)
                {
                    string strVal = (field.ReferencesId != null ? referenceValues[field.ReferencesId] : previousValue).ToString();
                    int value;
                    if (!int.TryParse(strVal, out value) || value < 0)
                    {
                        throw new Exception("List field referenced a non-integer or negative count: " + strVal);
                    }

                    Dictionary<object, object> list = new Dictionary<object, object>();

                    for (int i = 0; i < value; i++)
                    {
                        list.Add(i, Decode(reader, field.SubFields));
                    }

                    packet[previousName + ": " + strVal] = list;
                    newName = previousName + ": " + value;
                    packet.Remove(previousName);
                }

                if (field.Type != null)
                {
                    if (FieldDecoders.Decoders.ContainsKey(field.Type))
                    {
                        object value = FieldDecoders.Decoders[field.Type](reader);
                        packet[field.Name] = value;

                        previousName = field.Name;
                        previousValue = value;
                        if (field.ReferenceId != null)
                        {
                            referenceValues[field.ReferenceId] = value;
                        }

                    }
                    else if (FieldDecoders.CustomDecoders.ContainsKey(field.Type))
                    {
                        object value = Decode(reader, FieldDecoders.CustomDecoders[field.Type]);
                        packet[field.Name] = value;

                        previousName = field.Name;
                        previousValue = value;
                        if (field.ReferenceId != null)
                        {
                            referenceValues[field.ReferenceId] = value;
                        }
                    }
                    else
                    {
                        throw new Exception("Unknown type: " + field.Type);
                    }

                    if (field.Name == null)
                    {
                        if (packet[newName] is Dictionary<object, object> dict)
                        {
                            packet.Remove(newName);
                            foreach (object key2 in dict.Keys)
                            {
                                packet[key2] = dict[key2];
                            }
                        }
                    }
                }
            }

            return packet;
        }
    }
}
