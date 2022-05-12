using BedrockWireAuthDump.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public class ProxyAuthenticationResult
    {
        public BedrockTokenPair Tokens { get; set; }
        public string PrivateKey { get; set; }
        public string PublicKey { get; set; }
        public string[] MinecraftChain { get; set; }
    }
}
