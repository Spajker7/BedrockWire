﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models.PacketFields
{
    public class ValuePacketField : PacketField
    {
        public string Type { get; set; }
        public string? ReferenceId { get; set; }
    }
}
