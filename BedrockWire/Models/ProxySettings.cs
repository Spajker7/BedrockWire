using BedrockWireAuthDump.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public class AuthDataSerialized
    {
        public string[] Chain { get; set; }
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
    }

    public class ProxySettings
    {
        public AuthDataSerialized Auth { get; set; }
        public string RemoteServerAddress { get; set; }
        public int ProxyPort { get; set; }
    }
}
