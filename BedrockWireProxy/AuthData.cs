using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

namespace BedrockWireProxy
{
	public class AuthDataSerialized
	{
		public string[] Chain { get; set; }
		public string PublicKey { get; set; }
		public string PrivateKey { get; set; }
	}

	public class AuthData
	{
		private static AsymmetricCipherKeyPair DeserializeKeys(string serializedPrivate, string serializedPublic)
		{
			var privateKey = PrivateKeyFactory.CreateKey(Convert.FromBase64String(serializedPrivate));
			var publicKey = PublicKeyFactory.CreateKey(Convert.FromBase64String(serializedPublic));

			return new AsymmetricCipherKeyPair(publicKey, privateKey);
		}

		public string[] Chain { get; set; }
		public AsymmetricCipherKeyPair MinecraftKeyPair { get; set; }

		public AuthData(AuthDataSerialized serialized)
		{
			Chain = serialized.Chain;
			MinecraftKeyPair = DeserializeKeys(serialized.PrivateKey, serialized.PublicKey);
		}
	}
}
