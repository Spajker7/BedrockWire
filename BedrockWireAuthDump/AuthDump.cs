using BedrockWireAuthDump.Model;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace BedrockWireAuthDump
{
    public class AuthDump
    {
		private readonly XboxAuthService _xboxAuthService;

		public AuthDump()
		{
			_xboxAuthService = new XboxAuthService();
		}

		public async Task<(string userCode, string verificationUrl, string deviceCode)> RequestDeviceCode()
		{
			var result = await _xboxAuthService.StartDeviceAuthConnect();

			return (result.UserCode, result.VerificationUrl, result.DeviceCode);
		}

		public async Task<BedrockTokenPair> StartDeviceCodePolling(string deviceCode)
		{
			return await _xboxAuthService.DoDeviceCodeLogin(deviceCode);
		}

		public async Task<(bool success, string minecraftChain, AsymmetricCipherKeyPair minecraftKeyPair)> GetMinecraftChain(string accessToken, string deviceId)
		{
			return await _xboxAuthService.TryAuthenticate(accessToken, deviceId);
		}

		public static (string priv, string pub) SerializeKeys(AsymmetricCipherKeyPair keys)
		{
			PrivateKeyInfo privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keys.Private);
			byte[] serializedPrivateBytes = privateKeyInfo.ToAsn1Object().GetDerEncoded();
			string serializedPrivate = Convert.ToBase64String(serializedPrivateBytes);

			SubjectPublicKeyInfo publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keys.Public);
			byte[] serializedPublicBytes = publicKeyInfo.ToAsn1Object().GetDerEncoded();
			string serializedPublic = Convert.ToBase64String(serializedPublicBytes);

			return (serializedPrivate, serializedPublic);
		}

		public static AsymmetricCipherKeyPair DeserializeKeys(string serializedPrivate, string serializedPublic)
		{
			var privateKey = PrivateKeyFactory.CreateKey(Convert.FromBase64String(serializedPrivate));
			var publicKey = PublicKeyFactory.CreateKey(Convert.FromBase64String(serializedPublic));

			return new AsymmetricCipherKeyPair(publicKey, privateKey);
		}
	}
}