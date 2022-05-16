using BedrockWire.Core.Model.PacketFields;
using System.Collections.Generic;

namespace BedrockWire.Core.Model
{

    public class PacketDefinition
    {
        public string Name { get; set; }
        public List<PacketField> Fields { get; set; }
    }
}
