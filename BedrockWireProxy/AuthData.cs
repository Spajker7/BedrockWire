using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

namespace BedrockWireProxy
{
	public class AuthData
	{
		public string[] Chain { get; set; }
		public AsymmetricCipherKeyPair MinecraftKeyPair { get; set; }
	}
}
