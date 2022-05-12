﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models.PacketFields
{
    public class FlagsPacketField : PacketField
    {
        public string? ReferencesId { get; set; }
        public List<CasePacketField> SubFields { get; set; }
    }
}
