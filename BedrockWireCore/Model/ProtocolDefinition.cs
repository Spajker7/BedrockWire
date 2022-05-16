using BedrockWire.Core.Model.PacketFields;
using System.Collections.Generic;

namespace BedrockWire.Core.Model
{

    public class ProtocolDefinition
    {
        public Dictionary<string, List<PacketField>> CustomTypes { get; set; }
        public Dictionary<int, PacketDefinition> PacketDefinitions { get; set; }
    }
}
