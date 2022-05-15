#region LICENSE

// The contents of this file are subject to the Common Public Attribution
// License Version 1.0. (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// https://github.com/NiclasOlofsson/MiNET/blob/master/LICENSE.
// The License is based on the Mozilla Public License Version 1.1, but Sections 14
// and 15 have been added to cover use of software over a computer network and
// provide for limited attribution for the Original Developer. In addition, Exhibit A has
// been modified to be consistent with Exhibit B.
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// The Original Code is MiNET.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2020 Niclas Olofsson.
// All Rights Reserved.

#endregion

using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Jose;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace MiNET.Utils.Cryptography
{
    public static class CryptoUtils
	{
		public static byte[] DecodeBase64Url(this string input)
		{
			return Base64Url.Decode(input);
		}

		public static string EncodeBase64Url(this byte[] input)
		{
			return Base64Url.Encode(input);
		}

		public static byte[] DecodeBase64(this string input)
		{
			return Convert.FromBase64String(input);
		}

		public static string EncodeBase64(this byte[] input)
		{
			return Convert.ToBase64String(input);
		}

		public static byte[] ToDerEncoded(this ECDiffieHellmanPublicKey key)
		{
			byte[] asn = new byte[24] {0x30, 0x76, 0x30, 0x10, 0x6, 0x7, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x2, 0x1, 0x6, 0x5, 0x2b, 0x81, 0x4, 0x0, 0x22, 0x3, 0x62, 0x0, 0x4};

			return asn.Concat(key.ToByteArray().Skip(8)).ToArray();
		}

		//public static ECDiffieHellmanPublicKey FromDerEncoded(byte[] keyBytes)
		//{
		//	var clientPublicKeyBlob = FixPublicKey(keyBytes.Skip(23).ToArray());

		//	ECDiffieHellmanPublicKey clientKey = ECDiffieHellmanCngPublicKey.FromByteArray(clientPublicKeyBlob, CngKeyBlobFormat.EccPublicBlob);
		//	return clientKey;
		//}

		private static byte[] FixPublicKey(byte[] publicKeyBlob)
		{
			var keyType = new byte[] {0x45, 0x43, 0x4b, 0x33};
			var keyLength = new byte[] {0x30, 0x00, 0x00, 0x00};

			return keyType.Concat(keyLength).Concat(publicKeyBlob.Skip(1)).ToArray();
		}

		public static byte[] ImportECDsaCngKeyFromCngKey(byte[] inKey)
		{
			inKey[2] = 83;
			return inKey;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] Encrypt(ReadOnlyMemory<byte> payload, CryptoContext cryptoContext)
		{
			// hash
			int hashPoolLen = 8 + payload.Length + cryptoContext.Key.Length;
			var hashBufferPooled = ArrayPool<byte>.Shared.Rent(hashPoolLen);
			Span<byte> hashBuffer = hashBufferPooled.AsSpan();
			BitConverter.GetBytes(Interlocked.Increment(ref cryptoContext.SendCounter)).CopyTo(hashBuffer.Slice(0, 8));
			payload.Span.CopyTo(hashBuffer.Slice(8));
			cryptoContext.Key.CopyTo(hashBuffer.Slice(8 + payload.Length));
			using var hasher =  SHA256.Create();
			Span<byte> validationCheckSum = hasher.ComputeHash(hashBufferPooled, 0, hashPoolLen).AsSpan(0, 8);
			ArrayPool<byte>.Shared.Return(hashBufferPooled);

			IBufferedCipher cipher = cryptoContext.Encryptor;
			var encrypted = new byte[payload.Length + 8];
			int length = cipher.ProcessBytes(payload.ToArray(), encrypted, 0);
			cipher.ProcessBytes(validationCheckSum.ToArray(), encrypted, length);

			return encrypted;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlyMemory<byte> Decrypt(ReadOnlyMemory<byte> payload, CryptoContext cryptoContext)
		{
			IBufferedCipher cipher = cryptoContext.Decryptor;

			ReadOnlyMemory<byte> clear = cipher.ProcessBytes(payload.ToArray());
			//TODO: Verify hash!
			return clear.Slice(0, clear.Length - 8);
		}

		// CLIENT TO SERVER STUFF

		public static AsymmetricCipherKeyPair GenerateClientKey()
		{
			var generator = new ECKeyPairGenerator("ECDH");
			generator.Init(new ECKeyGenerationParameters(new DerObjectIdentifier("1.3.132.0.34"), SecureRandom.GetInstance("SHA256PRNG")));
			return generator.GenerateKeyPair();
		}

		private static ECDsa ConvertToSingKeyFormat(AsymmetricCipherKeyPair key)
		{
			ECPublicKeyParameters pubAsyKey = (ECPublicKeyParameters) key.Public;
			ECPrivateKeyParameters privAsyKey = (ECPrivateKeyParameters) key.Private;

			var signParam = new ECParameters
			{
				Curve = ECCurve.NamedCurves.nistP384,
				Q =
				{
					X = pubAsyKey.Q.AffineXCoord.GetEncoded(),
					Y = pubAsyKey.Q.AffineYCoord.GetEncoded()
				}
			};
			signParam.D = FixDSize(privAsyKey.D.ToByteArrayUnsigned(), signParam.Q.X.Length);
			signParam.Validate();

			return ECDsa.Create(signParam);
		}

		public static byte[] FixDSize(byte[] input, int expectedSize)
		{
			if (input.Length == expectedSize)
			{
				return input;
			}

			byte[] tmp;

			if (input.Length < expectedSize)
			{
				tmp = new byte[expectedSize];
				Buffer.BlockCopy(input, 0, tmp, expectedSize - input.Length, input.Length);
				return tmp;
			}

			if (input.Length > expectedSize + 1 || input[0] != 0)
			{
				throw new InvalidOperationException();
			}

			tmp = new byte[expectedSize];
			Buffer.BlockCopy(input, 1, tmp, 0, expectedSize);
			return tmp;
		}

		public static byte[] CompressJwtBytes(byte[] certChain, byte[] skinData, CompressionLevel compressionLevel)
		{
			using (MemoryStream stream = MiNetServer.MemoryStreamManager.GetStream())
			{
				{
					{
						byte[] lenBytes = BitConverter.GetBytes(certChain.Length);
						stream.Write(lenBytes, 0, lenBytes.Length);
						stream.Write(certChain, 0, certChain.Length);
					}
					{
						byte[] lenBytes = BitConverter.GetBytes(skinData.Length);
						stream.Write(lenBytes, 0, lenBytes.Length);
						stream.Write(skinData, 0, skinData.Length);
					}
				}

				var bytes = stream.ToArray();

				return bytes;
			}
		}
	}
}