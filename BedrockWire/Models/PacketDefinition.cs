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

    public class PacketDefinition
    {
        public string Name { get; set; }
        public List<PacketField> Fields { get; set; }
    }
}
