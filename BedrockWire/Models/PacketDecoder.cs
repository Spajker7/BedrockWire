using BedrockWire.Models.PacketFields;
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
            Dictionary<string, Tuple<string, object>> referenceValues = new Dictionary<string, Tuple<string, object>>();
            string previousName = null;
            object previousValue = null;

            foreach (PacketField field in packetFields)
            {
                string fieldName = field.Name;
                object fieldValue = null;

                if (field is ConditionalPacketField conditionalField)
                {
                    string condition = conditionalField.Condition;
                    bool isNegative = condition[0] == '!';
                    if (isNegative)
                    {
                        condition = condition.Substring(1);
                    }

                    object referencedValue = conditionalField.ReferencesId != null ? referenceValues[conditionalField.ReferencesId].Item2 : previousValue;
                    string referencedName = conditionalField.ReferencesId != null ? referenceValues[conditionalField.ReferencesId].Item1 : previousName;

                    if ((!isNegative && condition.Equals(referencedValue.ToString())) || (isNegative && !condition.Equals(referencedValue.ToString())))
                    {
                        fieldValue = Decode(reader, conditionalField.SubFields);
                        if (fieldName == null)
                        {
                            // replace referenced field
                            packet.Remove(referencedName);
                            fieldName = referencedName + ": " + referencedValue.ToString();
                        }
                    }
                }
                else if (field is SwitchPacketField switchField)
                {
                    object referencedValue = switchField.ReferencesId != null ? referenceValues[switchField.ReferencesId].Item2 : previousValue;
                    string referencedName = switchField.ReferencesId != null ? referenceValues[switchField.ReferencesId].Item1 : previousName;

                    CasePacketField? elm = switchField.SubFields.Where(x => x.Value.Equals(referencedValue.ToString())).LastOrDefault();

                    if (elm != null)
                    {
                        fieldValue = Decode(reader, elm.SubFields);
                        if (fieldName == null)
                        {
                            // replace referenced field
                            packet.Remove(referencedName);
                            fieldName = referencedName + ": " + referencedValue.ToString();
                        }
                    }
                }
                else if (field is FlagsPacketField flagsField)
                {
                    object referencedValue = flagsField.ReferencesId != null ? referenceValues[flagsField.ReferencesId].Item2 : previousValue;
                    string referencedName = flagsField.ReferencesId != null ? referenceValues[flagsField.ReferencesId].Item1 : previousName;

                    long value;
                    if (!long.TryParse(referencedValue.ToString(), out value))
                    {
                        throw new Exception("Flags field referenced a non-integer: " + referencedValue.ToString());
                    }

                    Dictionary<object, object> list = new Dictionary<object, object>();
                    int i = 0;

                    foreach (CasePacketField x in flagsField.SubFields)
                    {
                        long bitflag;

                        if (x.Value.StartsWith("0x"))
                        {
                            try
                            {
                                bitflag = Convert.ToInt32(x.Value, 16);
                            }
                            catch (FormatException)
                            {
                                throw new Exception("Bit flag field has bad hex: " + x.Value);
                            }
                        }
                        else if (!long.TryParse(x.Value, out bitflag))
                        {
                            throw new Exception("Bit flag field has bad number: " + x.Value);
                        }

                        if((value & bitflag) != 0)
                        {
                            list.Add(i, Decode(reader, x.SubFields));
                            i++;
                        }
                    }

                    fieldValue = list;
                    if (fieldName == null)
                    {
                        // replace referenced field
                        packet.Remove(referencedName);
                        fieldName = referencedName + ": " + referencedValue.ToString();
                    }
                }
                else if (field is ListPacketField listField)
                {
                    object referencedValue = listField.ReferencesId != null ? referenceValues[listField.ReferencesId].Item2 : previousValue;
                    string referencedName = listField.ReferencesId != null ? referenceValues[listField.ReferencesId].Item1 : previousName;

                    long value;
                    if (!long.TryParse(referencedValue.ToString(), out value) || value < 0)
                    {
                        throw new Exception("List field referenced a non-integer or negative count: " + referencedValue.ToString());
                    }

                    Dictionary<object, object> list = new Dictionary<object, object>();

                    for (int i = 0; i < value; i++)
                    {
                        list.Add(i, Decode(reader, listField.SubFields));
                    }

                    fieldValue = list;
                    if (fieldName == null)
                    {
                        // replace referenced field
                        packet.Remove(referencedName);
                        fieldName = referencedName + ": " + referencedValue.ToString();
                    }
                }
                else if (field is ValuePacketField valueField)
                {
                    if (FieldDecoders.Decoders.ContainsKey(valueField.Type))
                    {
                        object value = FieldDecoders.Decoders[valueField.Type](reader);
                        fieldValue = value;

                    }
                    else if (FieldDecoders.CustomDecoders.ContainsKey(valueField.Type))
                    {
                        object value = Decode(reader, FieldDecoders.CustomDecoders[valueField.Type]);
                        fieldValue = value;
                    }
                    else
                    {
                        throw new Exception("Unknown type: " + valueField.Type);
                    }

                    if (valueField.ReferenceId != null)
                    {
                        referenceValues[valueField.ReferenceId] = new Tuple<string, object>(fieldName, fieldValue);
                    }
                }

                if (fieldName == null)
                {
                    if (fieldValue is Dictionary<object, object> dict)
                    {
                        foreach (object key2 in dict.Keys)
                        {
                            packet[key2] = dict[key2];
                        }
                    }
                }
                else

                {
                    packet[fieldName] = fieldValue;
                }

                previousName = fieldName;
                previousValue = fieldValue;
            }

            return packet;
        }
    }
}
