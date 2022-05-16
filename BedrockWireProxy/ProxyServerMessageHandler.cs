using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
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
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SicStream;

namespace BedrockWireProxy
{
    public class ProxyServerMessageHandler : ICustomMessageHandler
	{
		public readonly RakSession Session;
		private protected ProxyClientMessageHandler _proxyClientMessageHandler;
		private readonly AuthData _authData;
		private readonly IPEndPoint remoteServer;
		private readonly IPacketWriter _packetWriter;

		public RakConnection ClientConnection { get; set; }
		public CryptoContext? CryptoContext { get; set; }
        public ulong StartTime { get; set; }

        public ProxyServerMessageHandler(RakSession session, IPEndPoint remoteServer, AuthData authData, IPacketWriter packetWriter)
		{
			Session = session;
			_authData = authData;
			this.remoteServer = remoteServer;
			_packetWriter = packetWriter;
		}

		public void Connected()
		{
		}

		public void Disconnect(string reason, bool sendDisconnect = true)
		{
		}

		public void Closed()
		{
			_proxyClientMessageHandler?.Session.Close();
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

				//var stream = new MemoryStreamReader(payload.Slice(0, payload.Length - 4)); // slice away adler
				//if (stream.ReadByte() != 0x78)
				//{
				//	if (Log.IsDebugEnabled) Log.Error($"Incorrect ZLib header. Expected 0x78 0x9C 0x{wrapper.Id:X2}\n{Packet.HexDump(wrapper.payload)}");
				//	if (Log.IsDebugEnabled) Log.Error($"Incorrect ZLib header. Decrypted 0x{wrapper.Id:X2}\n{Packet.HexDump(payload)}");
				//	throw new InvalidDataException("Incorrect ZLib header. Expected 0x78 0x9C");
				//}
				//stream.ReadByte();
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
				if (packet.Id == 0x01) // login packet, special case because of encryption
				{
					StartTime = time;
					_packetWriter.WritePacket(BedrockWireFormat.PacketDirection.Serverbound, packet.Id, packet.Payload, 0);

					MemoryStreamReader reader = new MemoryStreamReader(packet.Payload);
					int protocol = BinaryPrimitives.ReverseEndianness(reader.ReadInt32());
					int certDataLen = (int) VarInt.ReadUInt32(reader);
					ReadOnlyMemory<byte> certData = reader.Read(certDataLen);
					MemoryStreamReader certDataReader = new MemoryStreamReader(certData);

					int countCertData = certDataReader.ReadInt32();
					string certificateChain = Encoding.UTF8.GetString(certDataReader.Read(countCertData).ToArray());

					int countSkinData = certDataReader.ReadInt32();
					string skinData = Encoding.UTF8.GetString(certDataReader.Read(countSkinData).ToArray());

					{
						dynamic json = JObject.Parse(certificateChain);

						JArray chain = json.chain;

						string validationKey = null;
						string identityPublicKey = null;
						CertificateData certificateData = null;

						foreach (JToken token in chain)
						{
							IDictionary<string, dynamic> headers = JWT.Headers(token.ToString());

							// Mojang root x5u cert (string): MHYwEAYHKoZIzj0CAQYFK4EEACIDYgAE8ELkixyLcwlZryUQcu1TvPOmI2B7vX83ndnWRUaXm74wFfa5f/lwQNTfrLVHa2PmenpGI6JhIMUJaWZrjmMj90NoKNFSNBuKdm8rYiXsfaz3K36x/1U26HpG0ZxK/V1V

							if (!headers.ContainsKey("x5u"))
								continue;

							string x5u = headers["x5u"];

							if (identityPublicKey == null)
							{
								if (CertificateData.MojangRootKey.Equals(x5u, StringComparison.InvariantCultureIgnoreCase))
								{
								}
								else if (chain.Count > 1)
								{
									continue;
								}
								else if (chain.Count == 1)
								{
								}
							}
							else if (identityPublicKey.Equals(x5u))
							{
							}

							ECPublicKeyParameters x5KeyParam = (ECPublicKeyParameters) PublicKeyFactory.CreateKey(x5u.DecodeBase64());
							var signParam = new ECParameters
							{
								Curve = ECCurve.NamedCurves.nistP384,
								Q =
							{
								X = x5KeyParam.Q.AffineXCoord.GetEncoded(),
								Y = x5KeyParam.Q.AffineYCoord.GetEncoded()
							},
							};
							signParam.Validate();

							CertificateData data = JWT.Decode<CertificateData>(token.ToString(), ECDsa.Create(signParam));

							// Validate

							if (data != null)
							{
								identityPublicKey = data.IdentityPublicKey;

								if (CertificateData.MojangRootKey.Equals(x5u, StringComparison.InvariantCultureIgnoreCase))
								{
									validationKey = data.IdentityPublicKey;
								}
								else if (validationKey != null && validationKey.Equals(x5u, StringComparison.InvariantCultureIgnoreCase))
								{
									//TODO: Remove. Just there to be able to join with same XBL multiple times without crashing the server.
									//data.ExtraData.Identity = Guid.NewGuid().ToString();
									certificateData = data;
								}
								else
								{
									if (data.ExtraData == null)
										continue;

									// Self signed, make sure they don't fake XUID
									if (data.ExtraData.Xuid != null)
									{
										data.ExtraData.Xuid = null;
									}

									//TODO: Remove. Just there to be able to join with same XBL multiple times without crashing the server.
									//data.ExtraData.Identity = Guid.NewGuid().ToString();
									certificateData = data;
								}
							}
							else
							{
							}
						}

						//TODO: Implement disconnect here

						{
							CryptoContext = new CryptoContext
							{
								UseEncryption = true,
							};

							// Use bouncy to parse the DER key
							ECPublicKeyParameters remotePublicKey = (ECPublicKeyParameters) PublicKeyFactory.CreateKey(certificateData.IdentityPublicKey.DecodeBase64());

							var b64RemotePublicKey = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(remotePublicKey).GetEncoded().EncodeBase64();
							Debug.Assert(certificateData.IdentityPublicKey == b64RemotePublicKey);
							Debug.Assert(remotePublicKey.PublicKeyParamSet.Id == "1.3.132.0.34");

							var generator = new ECKeyPairGenerator("ECDH");
							generator.Init(new ECKeyGenerationParameters(remotePublicKey.PublicKeyParamSet, SecureRandom.GetInstance("SHA256PRNG")));
							var keyPair = generator.GenerateKeyPair();

							ECPublicKeyParameters pubAsyKey = (ECPublicKeyParameters) keyPair.Public;
							ECPrivateKeyParameters privAsyKey = (ECPrivateKeyParameters) keyPair.Private;

							var secretPrepend = Encoding.UTF8.GetBytes("RANDOM SECRET");

							ECDHBasicAgreement agreement = new ECDHBasicAgreement();
							agreement.Init(keyPair.Private);
							byte[] secret;
							using (var sha = SHA256.Create())
							{
								secret = sha.ComputeHash(secretPrepend.Concat(agreement.CalculateAgreement(remotePublicKey).ToByteArrayUnsigned()).ToArray());
							}

							Debug.Assert(secret.Length == 32);


							var encryptor = new StreamingSicBlockCipher(new SicBlockCipher(new AesEngine()));
							var decryptor = new StreamingSicBlockCipher(new SicBlockCipher(new AesEngine()));
							decryptor.Init(false, new ParametersWithIV(new KeyParameter(secret), secret.Take(12).Concat(new byte[] { 0, 0, 0, 2 }).ToArray()));
							encryptor.Init(true, new ParametersWithIV(new KeyParameter(secret), secret.Take(12).Concat(new byte[] { 0, 0, 0, 2 }).ToArray()));

							//IBufferedCipher decryptor = CipherUtilities.GetCipher("AES/CFB8/NoPadding");
							//decryptor.Init(false, new ParametersWithIV(new KeyParameter(secret), secret.Take(16).ToArray()));

							//IBufferedCipher encryptor = CipherUtilities.GetCipher("AES/CFB8/NoPadding");
							//encryptor.Init(true, new ParametersWithIV(new KeyParameter(secret), secret.Take(16).ToArray()));

							CryptoContext.Key = secret;
							CryptoContext.Decryptor = decryptor;
							CryptoContext.Encryptor = encryptor;

							var signParam = new ECParameters
							{
								Curve = ECCurve.NamedCurves.nistP384,
								Q =
								{
									X = pubAsyKey.Q.AffineXCoord.GetEncoded(),
									Y = pubAsyKey.Q.AffineYCoord.GetEncoded()
								}
							};
							signParam.D = CryptoUtils.FixDSize(privAsyKey.D.ToByteArrayUnsigned(), signParam.Q.X.Length);
							signParam.Validate();

							string? signedToken = null;
							var signKey = ECDsa.Create(signParam);
							var b64PublicKey = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pubAsyKey).GetEncoded().EncodeBase64();
							var handshakeJson = new HandshakeData
							{
								salt = secretPrepend.EncodeBase64(),
								signedToken = signedToken
							};
							string val = JWT.Encode(handshakeJson, signKey, JwsAlgorithm.ES384, new Dictionary<string, object> { { "x5u", b64PublicKey } });


							MemoryStream ms = new MemoryStream();
							byte[] bytes = Encoding.UTF8.GetBytes(val);
							VarInt.WriteUInt32(ms, (uint) bytes.Length);
							ms.Write(bytes);

							var response = new RawMinecraftPacket(0x03, ms.ToArray().AsMemory());
							response.ForceClear = true;

							Session.SendPacket(response);

							// start connection to proxy
							var greyListManager = new GreyListManager();
							var motdProvider = new MotdProvider();

							ClientConnection = new RakConnection(new IPEndPoint(IPAddress.Any, 0), greyListManager, motdProvider);
							ClientConnection.CustomMessageHandlerFactory = session => {
								var handler = new ProxyClientMessageHandler(session, _authData, protocol, JWT.Payload(skinData), this, _packetWriter, StartTime);
								_proxyClientMessageHandler = handler;
								return handler;
							};

							//TODO: This is bad design, need to refactor this later.
							greyListManager.ConnectionInfo = ClientConnection.ConnectionInfo;
							var serverInfo = ClientConnection.ConnectionInfo;
							serverInfo.MaxNumberOfPlayers = 1;
							serverInfo.MaxNumberOfConcurrentConnects = 1;

							ClientConnection.Start();
							ClientConnection.TryConnect(remoteServer, 1);
						}
					}
				}
				else
				{
					_packetWriter.WritePacket(BedrockWireFormat.PacketDirection.Serverbound, packet.Id, packet.Payload, time - StartTime);
					_proxyClientMessageHandler.Session.SendPacket(packet);
				}
			}
			
		}
	}
}
