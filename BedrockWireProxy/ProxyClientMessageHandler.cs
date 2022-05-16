using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BedrockWireProxyInterface;
using Jose;
using MiNET;
using MiNET.Net;
using MiNET.Net.RakNet;
using MiNET.Utils;
using MiNET.Utils.Cryptography;
using MiNET.Utils.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SicStream;

namespace BedrockWireProxy
{
    class JWTMapper : IJsonMapper
	{
		private static DefaultContractResolver ContractResolver = new DefaultContractResolver
		{
			NamingStrategy = new CamelCaseNamingStrategy()
		};

		public string Serialize(object obj)
		{
			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Include,
				//    ContractResolver = ContractResolver
			};

			return JsonConvert.SerializeObject(obj, Formatting.Indented, settings);
		}

		public T Parse<T>(string json)
		{
			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Include,
				//  ContractResolver = ContractResolver
			};

			return JsonConvert.DeserializeObject<T>(json, settings);
		}
	}

	public class ProxyClientMessageHandler : ICustomMessageHandler
	{
		public readonly RakSession Session;
		private protected readonly int _protocol;
		private protected readonly string _skinData;
		private protected readonly ProxyServerMessageHandler _proxyServerMessageHandler;
		private protected readonly AuthData _authData;
		private protected readonly IPacketWriter _packetWriter;
        public ulong StartTime { get; set; }

        public CryptoContext? CryptoContext { get; set; }

		public ProxyClientMessageHandler(RakSession session, AuthData authData, int protocol, string skinData, ProxyServerMessageHandler proxyServerMessageHandler, IPacketWriter packetWriter, ulong startTime)
		{
			Session = session;
			_authData = authData;
			_protocol = protocol;
			_skinData = skinData;
			_proxyServerMessageHandler = proxyServerMessageHandler;
			_packetWriter = packetWriter;
			StartTime = startTime;
		}

		private static ECDsa ConvertToSingKeyFormat(AsymmetricCipherKeyPair key)
		{
			ECPublicKeyParameters pubAsyKey = (ECPublicKeyParameters) key.Public;
			ECPrivateKeyParameters privAsyKey = (ECPrivateKeyParameters) key.Private;

			var signParam = new ECParameters
			{
				Curve = ECCurve.NamedCurves.nistP384,
				Q = { X = pubAsyKey.Q.AffineXCoord.GetEncoded(), Y = pubAsyKey.Q.AffineYCoord.GetEncoded() }
			};

			signParam.D = CryptoUtils.FixDSize(privAsyKey.D.ToByteArrayUnsigned(), signParam.Q.X.Length);
			signParam.Validate();

			return ECDsa.Create(signParam);
		}

		public void Connected()
		{
			// send login
			var clientKey = _authData.MinecraftKeyPair; // CryptoUtils.GenerateClientKey();

			ECDsa signKey = ConvertToSingKeyFormat(clientKey);

			string b64Key = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(clientKey.Public).GetEncoded()
			   .EncodeBase64();

			string identity, xuid = "";
			byte[] certChain = null;

			//MinecraftChain = What i get back from the XBOXLive auth
			if (_authData.Chain != null)
			{
				var chain = _authData.Chain;
				IDictionary<string, dynamic> chainHeader = JWT.Headers(chain[0]);
				string x5u = chainHeader["x5u"];

				long iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				long exp = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();

				string val = JWT.Encode(
					new { certificateAuthority = true, exp = exp, identityPublicKey = x5u, nbf = iat }, signKey,
					JwsAlgorithm.ES384, new Dictionary<string, object> { { "x5u", b64Key } },
					new JwtSettings() { JsonMapper = new JWTMapper() });

				chain = new string[] { val, chain[0], chain[1] };

				var hack = new { chain = chain };

				var jsonChain = JsonConvert.SerializeObject(hack);

				// construct login packet
				certChain = Encoding.UTF8.GetBytes(jsonChain); // XblmsaService.MinecraftChain;
															   //File.WriteAllBytes("/home/kenny/xbox.json", certChain);

				string skinDataValue = JWT.Encode(_skinData, signKey, JwsAlgorithm.ES384, new Dictionary<string, object> { { "x5u", b64Key } }, new JwtSettings() { JsonMapper = new JWTMapper() });
				byte[] skinDataBytes = Encoding.UTF8.GetBytes(skinDataValue);

				MemoryStream stream = new MemoryStream();
				BinaryWriter writer = new BinaryWriter(stream);
				writer.Write(BinaryPrimitives.ReverseEndianness(_protocol));

				MemoryStream stream2 = new MemoryStream();
				{
					byte[] lenBytes = BitConverter.GetBytes(certChain.Length);
					stream2.Write(lenBytes, 0, lenBytes.Length);
					stream2.Write(certChain, 0, certChain.Length);
				}

				{
					byte[] lenBytes = BitConverter.GetBytes(skinDataBytes.Length);
					stream2.Write(lenBytes, 0, lenBytes.Length);
					stream2.Write(skinDataBytes, 0, skinDataBytes.Length);
				}

				byte[] data = stream2.ToArray();

				VarInt.WriteUInt32(stream, (uint) stream2.Length);
				stream.Write(data);

				var response = new RawMinecraftPacket(0x01, stream.ToArray().AsMemory());
				response.ForceClear = true;

				Session.SendPacket(response);

				CryptoContext = new CryptoContext() { ClientKey = clientKey, UseEncryption = false, };
			}
		}

		public void Closed()
		{
			_proxyServerMessageHandler?.Session.Close();
		}

		public void Disconnect(string reason, bool sendDisconnect = true)
		{
			
		}

		public List<Packet> PrepareSend(List<Packet> packetsToSend)
		{
			var sendList = new List<Packet>();
			var sendInBatch = new List<Packet>();

			foreach (Packet packet in packetsToSend)
			{
				// We must send forced clear messages in single message batch because
				// we can't mix them with un-encrypted messages for obvious reasons.
				// If need be, we could put these in a batch of it's own, but too rare 
				// to bother.
				if (packet.ForceClear)
				{
					var wrapper = McpeWrapper.CreateObject();
					wrapper.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;
					wrapper.ForceClear = true;
					wrapper.payload = Compression.CompressPacketsForWrapper(new List<Packet> { packet });
					wrapper.Encode(); // prepare
					packet.PutPool();
					sendList.Add(wrapper);
					continue;
				}

				if (packet is McpeWrapper)
				{
					packet.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;
					sendList.Add(packet);
					continue;
				}

				if (!packet.IsMcpe)
				{
					packet.ReliabilityHeader.Reliability = packet.ReliabilityHeader.Reliability != Reliability.Undefined ? packet.ReliabilityHeader.Reliability : Reliability.Reliable;
					sendList.Add(packet);
					continue;
				}

				packet.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;

				sendInBatch.Add(OnSendCustomPacket(packet));
			}

			if (sendInBatch.Count > 0)
			{
				var batch = McpeWrapper.CreateObject();
				batch.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;
				batch.payload = Compression.CompressPacketsForWrapper(sendInBatch);
				batch.Encode(); // prepare
				sendList.Add(batch);
			}

			return sendList;
		}

		public Packet HandleOrderedSend(Packet packet)
		{
			if (!packet.ForceClear && CryptoContext != null && CryptoContext.UseEncryption && packet is McpeWrapper wrapper)
			{
				var encryptedWrapper = McpeWrapper.CreateObject();
				encryptedWrapper.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;
				encryptedWrapper.payload = CryptoUtils.Encrypt(wrapper.payload, CryptoContext);
				encryptedWrapper.Encode();

				return encryptedWrapper;
			}

			return packet;
		}

		public void HandlePacket(Packet message)
		{
			if (message == null)
				throw new NullReferenceException();

			if (message is McpeWrapper wrapper)
			{
				var messages = new List<Packet>();

				// Get bytes to process
				ReadOnlyMemory<byte> payload = wrapper.payload;

				// Decrypt bytes

				if (CryptoContext != null && CryptoContext.UseEncryption)
				{
					// This call copies the entire buffer, but what can we do? It is kind of compensated by not
					// creating a new buffer when parsing the packet (only a mem-slice)
					payload = CryptoUtils.Decrypt(payload, CryptoContext);
				}

				// Decompress bytes
				var stream = new MemoryStreamReader(payload);
				try
				{
					using (var deflateStream = new DeflateStream(stream, CompressionMode.Decompress, false))
					{
						using var s = new MemoryStream();
						deflateStream.CopyTo(s);
						s.Position = 0;

						int count = 0;
						// Get actual packet out of bytes
						while (s.Position < s.Length)
						{
							count++;

							uint len = VarInt.ReadUInt32(s);
							long pos = s.Position;
							ReadOnlyMemory<byte> internalBuffer = s.GetBuffer().AsMemory((int) s.Position, (int) len);
							try
							{
								var packet = new RawMinecraftPacket();
								packet.Decode(internalBuffer);
								messages.Add(packet);
							}
							catch (Exception e)
							{
								return; // Exit, but don't crash.
							}

							s.Position = pos + len;
						}

						if (s.Length > s.Position)
							throw new Exception("Have more data");
					}
				}
				catch (Exception e)
				{
					throw;
				}

				foreach (Packet msg in messages)
				{
					// Temp fix for performance, take 1.
					//var interact = msg as McpeInteract;
					//if (interact?.actionId == 4 && interact.targetRuntimeEntityId == 0) continue;

					msg.ReliabilityHeader = new ReliabilityHeader()
					{
						Reliability = wrapper.ReliabilityHeader.Reliability,
						ReliableMessageNumber = wrapper.ReliabilityHeader.ReliableMessageNumber,
						OrderingChannel = wrapper.ReliabilityHeader.OrderingChannel,
						OrderingIndex = wrapper.ReliabilityHeader.OrderingIndex,
					};

					try
					{
						HandleCustomPacket(msg);
					}
					catch (Exception e)
					{
					}
				}

				wrapper.PutPool();
			}
			else if (message is UnknownPacket unknownPacket)
			{
				unknownPacket.PutPool();
			}
		}

		public Packet OnSendCustomPacket(Packet message)
		{
			return message;
		}

		public void HandleCustomPacket(Packet message)
		{
			if(message is RawMinecraftPacket packet)
			{
				var time = (ulong)DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
				if (packet.Id == 0x03)
				{
					_packetWriter.WritePacket(BedrockWireFormat.PacketDirection.Clientbound, packet.Id, packet.Payload, time - StartTime);
					MemoryStreamReader reader = new MemoryStreamReader(packet.Payload);
					int len = (int) VarInt.ReadUInt32(reader);
					string token = Encoding.UTF8.GetString(reader.Read((ulong) len).ToArray());

					IDictionary<string, dynamic> headers = JWT.Headers(token);
					string x5u = headers["x5u"].TrimEnd('=');

					var data = JWT.Payload<HandshakeData>(token);
					var serverKey = Base64Url.Decode(x5u);
					var randomKeyToken = Base64Url.Decode(data.salt.TrimEnd('='));

					ECPublicKeyParameters remotePublicKey = (ECPublicKeyParameters) PublicKeyFactory.CreateKey(serverKey);

					ECDHBasicAgreement agreement = new ECDHBasicAgreement();
					agreement.Init(CryptoContext.ClientKey.Private);
					byte[] secret;

					using (var sha = SHA256.Create())
					{
						secret = sha.ComputeHash(
							randomKeyToken.Concat(agreement.CalculateAgreement(remotePublicKey).ToByteArrayUnsigned())
								.ToArray());
					}

					// Create a decrytor to perform the stream transform.

					var encryptor = new StreamingSicBlockCipher(new SicBlockCipher(new AesEngine()));
					var decryptor = new StreamingSicBlockCipher(new SicBlockCipher(new AesEngine()));

					decryptor.Init(
						false,
						new ParametersWithIV(
							new KeyParameter(secret), secret.Take(12).Concat(new byte[] { 0, 0, 0, 2 }).ToArray()));

					encryptor.Init(
						true,
						new ParametersWithIV(
							new KeyParameter(secret), secret.Take(12).Concat(new byte[] { 0, 0, 0, 2 }).ToArray()));

					CryptoContext.Decryptor = decryptor;
					CryptoContext.Encryptor = encryptor;
					CryptoContext.Key = secret;
					CryptoContext.UseEncryption = true;
				}
				else
				{
					_packetWriter.WritePacket(BedrockWireFormat.PacketDirection.Clientbound, packet.Id, packet.Payload, time - StartTime);
					_proxyServerMessageHandler.Session.SendPacket(packet);
				}
			}
		}
    }
}
