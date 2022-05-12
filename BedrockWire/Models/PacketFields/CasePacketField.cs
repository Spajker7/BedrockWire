using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models.PacketFields
{
    public class CasePacketField : PacketField
    {
        public string Value { get; set; }
        public List<PacketField> SubFields { get; set; }
    }
}
