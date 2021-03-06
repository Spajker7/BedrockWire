using AdaptiveExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Core.Model.PacketFields
{
    public class ConditionalPacketField : PacketField
    {
        public Expression Condition { get; set; }
        public string? ReferencesId { get; set; }
        public List<PacketField> SubFields { get; set; }
    }
}
