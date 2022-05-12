using BedrockWireProxy;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

namespace BedrockWireProxyCli
{
	public class AuthDataSerialized
	{
		public string[] Chain { get; set; }
		public string PublicKey { get; set; }
		public string PrivateKey { get; set; }

		public AuthData ToAuthData()
        {
			AuthData authData = new AuthData();
			authData.Chain = Chain;

			var privateKey = PrivateKeyFactory.CreateKey(Convert.FromBase64String(PrivateKey));
			var publicKey = PublicKeyFactory.CreateKey(Convert.FromBase64String(PublicKey));

			authData.MinecraftKeyPair = new AsymmetricCipherKeyPair(publicKey, privateKey);

			return authData;
        }
	}
}
