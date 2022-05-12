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
                return xp.OrderId.CompareTo(yp.OrderId);
            }
            throw new NotImplementedException();
        }
    }
}
