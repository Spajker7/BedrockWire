using BedrockWire.Core.Model;
using BedrockWire.Models;
using System;
using System.Collections;

namespace BedrockWire.Utils
{
    public class PacketComparer : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if(x is Packet xp && y is Packet yp)
            {
                return xp.Index.CompareTo(yp.Index);
            }
            throw new NotImplementedException();
        }
    }
}
