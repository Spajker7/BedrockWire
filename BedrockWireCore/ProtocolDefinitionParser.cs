using BedrockWire.Core.Model;
using BedrockWire.Core.Model.PacketFields;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace BedrockWire.Core
{
    public static class ProtocolDefinitionParser
    {
        public static ProtocolDefinition Parse(string fileName)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);

            return Parse(doc);
        }

        public static ProtocolDefinition Parse(XmlDocument doc)
        {
            var protocolDefinition = new ProtocolDefinition();
            protocolDefinition.CustomTypes = new Dictionary<string, List<PacketField>>();
            protocolDefinition.PacketDefinitions = new Dictionary<int, PacketDefinition>();

            foreach (XmlNode node in doc.DocumentElement.SelectNodes("child::types")[0])
            {
                if (node.Name == "type")
                {
                    string name = node.Attributes.GetNamedItem("name").InnerText;

                    List<PacketField> fields = new List<PacketField>();

                    foreach (XmlNode fieldNode in node.ChildNodes)
                    {
                        if (fieldNode.NodeType == XmlNodeType.Element)
                        {
                            fields.Add(ParseField(fieldNode));
                        }
                    }

                    protocolDefinition.CustomTypes.Add(name, fields);
                }
            }

            foreach (XmlNode node in doc.DocumentElement.SelectNodes("child::packets")[0])
            {
                if (node.Name == "packet")
                {
                    int id = Convert.ToInt32(node.Attributes.GetNamedItem("id").InnerText, 16);
                    string name = node.Attributes.GetNamedItem("name").InnerText;

                    List<PacketField> fields = new List<PacketField>();

                    foreach (XmlNode fieldNode in node.ChildNodes)
                    {
                        if (fieldNode.NodeType == XmlNodeType.Element)
                        {
                            fields.Add(ParseField(fieldNode));
                        }
                    }

                    protocolDefinition.PacketDefinitions.Add(id, new PacketDefinition() { Name = name, Fields = fields });
                }
            }

            return protocolDefinition;
        }

        private static PacketField ParseField(XmlNode node)
        {
            string name = node.Name;
            switch (name)
            {
                case "switch":
                    return new SwitchPacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                        SubFields = new List<CasePacketField>(GetSubFields(node).Cast<CasePacketField>())
                    };
                case "list":
                    return new ListPacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                        SubFields = GetSubFields(node)
                    };
                case "flags":
                    return new FlagsPacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                        SubFields = new List<CasePacketField>(GetSubFields(node).Cast<CasePacketField>())
                    };
                case "case":
                    return new CasePacketField()
                    {
                        Value = node.Attributes.GetNamedItem("value").InnerText,
                        SubFields = GetSubFields(node)
                    };
                case "conditional":
                    // support for simpler type of expressions
                    string condition = node.Attributes.GetNamedItem("condition").InnerText;
                    if (!condition.Contains("value"))
                    {
                        bool isNegative = condition[0] == '!';
                        if (isNegative)
                        {
                            condition = condition.Substring(1);
                        }

                        condition = "value" + (isNegative ? " != " : " == ") + condition;
                    }

                    return new ConditionalPacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                        Condition = condition,
                        SubFields = GetSubFields(node),
                    };
                case "field":
                    return new ValuePacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        Type = node.Attributes.GetNamedItem("type")?.InnerText,
                        ReferenceId = node.Attributes.GetNamedItem("refId")?.InnerText
                    };
                default:
                    throw new Exception("Unknown field type: " + name);

            }
        }

        private static List<PacketField> GetSubFields(XmlNode node)
        {
            List<PacketField> list = new List<PacketField>();

            foreach (XmlNode fieldNode in node.ChildNodes)
            {
                if (fieldNode.NodeType == XmlNodeType.Element)
                {
                    list.Add(ParseField(fieldNode));
                }

            }

            return list;
        }
    }
}
