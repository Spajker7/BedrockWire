using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public class PacketField
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsSwitch { get; set; }
        public string Conditional { get; set; }
        public string Case { get; set; }
        public List<PacketField> SubFields { get; set; }
        public bool IgnoreContainer { get; set; }
    }
}
