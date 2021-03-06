using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using BedrockWireAuthDump.Model;
using Jose;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace BedrockWireAuthDump
{
	public class XboxAuthService
	{
		public const string MSA_CLIENT_ID = "android-app://com.mojang.minecraftpe.H62DKCBHJP6WXXIV7RBFOGOL4NAK4E6Y";
		public const string MSA_COBRAND_ID = "90023";
		public const string PLATFORM_NAME = "android2.1.0504.0524";

		private const string LoginWithXbox = "https://api.minecraftservices.com/authentication/login_with_xbox";

		private const string UserAuth = "https://user.auth.xboxlive.com/user/authenticate";
		private const string DeviceAuth = "https://device.auth.xboxlive.com/device/authenticate";
		private const string TitleAuth = "https://title.auth.xboxlive.com/title/authenticate";
		private const string XblAuth = "https://xsts.auth.xboxlive.com/xsts/authorize";
		private const string MinecraftAuthUrl = "https://multiplayer.minecraft.net/authentication";

		//private const string ClientId = "0000000048183522";
		private const string ClientId = "00000000441cc96b";

		public const string
			AuthorizationUri = "https://login.live.com/oauth20_authorize.srf"; // Authorization code endpoint

		private const string RedirectUri = "https://login.live.com/oauth20_desktop.srf"; // Callback endpoint
		private const string RefreshUri = "https://login.live.com/oauth20_token.srf"; // Get tokens endpoint
		private string X { get; set; }
		private string Y { get; set; }

		private readonly HttpClient _httpClient;
		private readonly AsymmetricCipherKeyPair _authKeyPair;

		public XboxAuthService()
		{
			_authKeyPair = GenerateKeys();

			ECPublicKeyParameters pubAsyKey = (ECPublicKeyParameters) _authKeyPair.Public;
			X = UrlSafe(pubAsyKey.Q.AffineXCoord.GetEncoded());
			Y = UrlSafe(pubAsyKey.Q.AffineYCoord.GetEncoded());

			var cookieContainer = new CookieContainer();

			var clienthandler = new HttpClientHandler
			{
				AllowAutoRedirect = true,
				UseCookies = true,
				CookieContainer = cookieContainer
			};

			_httpClient = new HttpClient(clienthandler);
			_httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
			_httpClient.DefaultRequestHeaders.Add("x-xbl-contract-version", "1");
		}

		private static AsymmetricCipherKeyPair GenerateKeys()
		{
			var curve = NistNamedCurves.GetByName("P-256");
			var domainParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

			var secureRandom = new SecureRandom();
			var keyParams = new ECKeyGenerationParameters(domainParams, secureRandom);

			var generator = new ECKeyPairGenerator("ECDSA");
			generator.Init(keyParams);

			return generator.GenerateKeyPair();
		}

		static readonly char[] padding = { '=' };

		private static string UrlSafe(byte[] a)
		{
			return System.Convert.ToBase64String(a).TrimEnd(padding).Replace('+', '-').Replace('/', '_');
		}

		private class MCChainPostData
		{
			[JsonProperty("identityPublicKey")] public string IdentityPublicKey { get; set; }
		}


		public async Task<(bool Success, string? MinecraftChain)> RequestMinecraftChain(HttpClient client, AuthResponse<XuiDisplayClaims<XstsXui>> token, AsymmetricCipherKeyPair MinecraftKeyPair)
		{
			var b = Convert.ToBase64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(MinecraftKeyPair.Public).GetEncoded());

			var body = new MCChainPostData() { IdentityPublicKey = b };

			using (var r = new HttpRequestMessage(HttpMethod.Post, MinecraftAuthUrl))
			{
				r.Headers.Add("Authorization", $"XBL3.0 x={token.DisplayClaims.Xui[0].UserHash};{token.Token}");
				//r.Headers.Add("User-Agent", "MCPE/Android");
				r.Headers.Add("Client-Version", "503");

				SetHeadersAndContent(r, body);

				try
				{
					using (var response = await client.SendAsync(r, HttpCompletionOption.ResponseContentRead)
							  .ConfigureAwait(false))
					{
						response.EnsureSuccessStatusCode();

						var rawResponse = await response.Content.ReadAsByteArrayAsync();
						return (true, Encoding.UTF8.GetString(rawResponse));
					}
				}
				catch (Exception)
				{
					return (false, null);
				}
			}
		}

		private async Task<AuthResponse<XuiDisplayClaims<XstsXui>>> DoXsts(HttpClient client,
			AuthResponse<DeviceDisplayClaims> deviceToken,
			AuthResponse<TitleDisplayClaims> title,
			string userToken,
			string relyingParty = "https://multiplayer.minecraft.net/")
		{
			//var key = EcDsa.ExportParameters(false);
			var authRequest = new AuthRequest
			{
				RelyingParty = relyingParty,
				TokenType = "JWT",
				Properties = new Dictionary<string, object>()
				{
					{ "UserTokens", new string[] { userToken } },
					{ "DeviceToken", deviceToken.Token },
					{ "TitleToken", title.Token },
					{ "SandboxId", "RETAIL" },
					{ "ProofKey", new ProofKey(X, Y) }
				}
			};


			using (var r = new HttpRequestMessage(HttpMethod.Post, XblAuth))
			{
				r.Headers.Add("User-Agent", "MCPE/Android");
				r.Headers.Add("Client-Version", "503");
				SetHeadersAndContent(r, authRequest);

				using (var response = await client.SendAsync(r, HttpCompletionOption.ResponseContentRead)
						  .ConfigureAwait(false))
				{
					response.EnsureSuccessStatusCode();

					var rawResponse = await response.Content.ReadAsStringAsync();

					//  Console.WriteLine(rawResponse);
					//  Console.WriteLine();
					return JsonConvert.DeserializeObject<AuthResponse<XuiDisplayClaims<XstsXui>>>(rawResponse);

					//Log.Debug($"Xsts Auth: {rawResponse}");
				}
			}
		}

		private async Task<AuthResponse<TitleDisplayClaims>> DoTitleAuth(HttpClient client,
			AuthResponse<DeviceDisplayClaims> deviceToken,
			string accessToken)
		{
			var authRequest = new AuthRequest
			{
				RelyingParty = "http://auth.xboxlive.com",
				TokenType = "JWT",
				Properties = new Dictionary<string, object>()
				{
					{ "AuthMethod", "RPS" },
					{ "DeviceToken", deviceToken.Token },
					{ "RpsTicket", $"t={accessToken}" },
					{ "SiteName", "user.auth.xboxlive.com" },
					{ "ProofKey", new ProofKey(X, Y) }
				}
			};

			using (var r = new HttpRequestMessage(HttpMethod.Post, TitleAuth))
			{
				SetHeadersAndContent(r, authRequest);

				using (var response = await client.SendAsync(r, HttpCompletionOption.ResponseContentRead)
						  .ConfigureAwait(false))
				{
					response.EnsureSuccessStatusCode();

					return JsonConvert.DeserializeObject<AuthResponse<TitleDisplayClaims>>(await response.Content.ReadAsStringAsync());
				}
			}
		}

		public async Task<AuthResponse<XuiDisplayClaims<Xui>>> AuthenticateWithXBL(HttpClient client,
			string accessToken)
		{
			//var key = EcDsa.ExportParameters(false);

			var authRequest = new AuthRequest
			{
				RelyingParty = "http://auth.xboxlive.com",
				TokenType = "JWT",
				Properties = new Dictionary<string, object>()
				{
					{ "AuthMethod", "RPS" },
					{ "RpsTicket", "d=" + accessToken },
					{ "SiteName", "user.auth.xboxlive.com" }
				}
			};

			using (var r = new HttpRequestMessage(HttpMethod.Post, UserAuth))
			{
				SetHeadersAndContent(r, authRequest);

				using (var response = await client.SendAsync(r, HttpCompletionOption.ResponseContentRead)
						  .ConfigureAwait(false))
				{
					response.EnsureSuccessStatusCode();

					return JsonConvert.DeserializeObject<AuthResponse<XuiDisplayClaims<Xui>>>(
						await response.Content.ReadAsStringAsync());
				}
			}
		}

		public async Task<AuthResponse<XuiDisplayClaims<Xui>>> ObtainUserToken(HttpClient client, string accessToken)
		{
			//var key = EcDsa.ExportParameters(false);

			var authRequest = new AuthRequest
			{
				RelyingParty = "http://auth.xboxlive.com",
				TokenType = "JWT",
				Properties = new Dictionary<string, object>()
				{
					{ "AuthMethod", "RPS" },
					{ "RpsTicket", "t=" + accessToken },
					{ "SiteName", "user.auth.xboxlive.com" },
					{ "ProofKey", new ProofKey(X, Y) }
				}
			};

			using (var r = new HttpRequestMessage(HttpMethod.Post, UserAuth))
			{
				SetHeadersAndContent(r, authRequest);

				using (var response = await client.SendAsync(r, HttpCompletionOption.ResponseContentRead)
						  .ConfigureAwait(false))
				{
					response.EnsureSuccessStatusCode();

					return JsonConvert.DeserializeObject<AuthResponse<XuiDisplayClaims<Xui>>>(
						await response.Content.ReadAsStringAsync());
				}
			}
		}

		private void SetHeadersAndContent(HttpRequestMessage request, object data)
		{
			request.Headers.Accept.ParseAdd("application/json");

			request.Content = SetHttpContent(data, out var jsonData);
			Sign(request, jsonData);
		}

		private async Task<AuthResponse<DeviceDisplayClaims>> ObtainDeviceToken(HttpClient client, string deviceId)
		{
			//  var id = Guid.NewGuid().ToString();

			var authRequest = new AuthRequest
			{
				RelyingParty = "http://auth.xboxlive.com",
				TokenType = "JWT",
				Properties = new Dictionary<string, object>()
				{
					{ "AuthMethod", "ProofOfPossession" },
					{ "Id", deviceId },
					{ "DeviceType", "Nintendo" },
					{ "SerialNumber", Guid.NewGuid().ToString() },
					{ "Version", "0.0.0.0" },
					{ "ProofKey", new ProofKey(X, Y) }
				}
			};

			var r = new HttpRequestMessage(HttpMethod.Post, DeviceAuth);

			{
				SetHeadersAndContent(r, authRequest);

				using (var response = await client.SendAsync(r, HttpCompletionOption.ResponseContentRead)
						  .ConfigureAwait(false))
				{
					var resp = await response.Content.ReadAsStringAsync();

					response.EnsureSuccessStatusCode();

					//Console.WriteLine($"Device Response: " + resp);

					return JsonConvert.DeserializeObject<AuthResponse<DeviceDisplayClaims>>(resp);
				}
			}
		}

		private void Sign(HttpRequestMessage request, byte[] body)
		{
			//var hash = SHA256.Create();

			var time = TimeStamp();
			byte[] p = new byte[8];
			p[0] = (byte) (time >> 56);
			p[1] = (byte) (time >> 48);
			p[2] = (byte) (time >> 40);
			p[3] = (byte) (time >> 32);
			p[4] = (byte) (time >> 24);
			p[5] = (byte) (time >> 16);
			p[6] = (byte) (time >> 8);
			p[7] = (byte) time;

			//signer.

			byte[] signed;

			using (MemoryStream buffer = new MemoryStream())
			{
				buffer.WriteByte(0);
				buffer.WriteByte(0);
				buffer.WriteByte(0);
				buffer.WriteByte(1);
				buffer.WriteByte(0);

				//Write time
				buffer.Write(p, 0, p.Length);

				buffer.WriteByte(0);

				//using (BinaryWriter writer = new BinaryWriter(buffer, Encoding.UTF8))
				{
					buffer.Write(Encoding.UTF8.GetBytes("POST"));
					buffer.WriteByte((byte) 0);

					buffer.Write(Encoding.UTF8.GetBytes(request.RequestUri.PathAndQuery));
					buffer.WriteByte((byte) 0);

					buffer.Write(Encoding.UTF8.GetBytes(request.Headers?.Authorization?.ToString() ?? ""));
					buffer.WriteByte((byte) 0);

					buffer.Write(body);
					buffer.WriteByte((byte) 0);
				}

				byte[] input = buffer.ToArray();
				signed = SignData(input, _authKeyPair.Private);
				//signed = EcDsa.SignHash(hash.ComputeHash(input));
			}

			byte[] final;
			;

			using (MemoryStream ms = new MemoryStream())
			{
				ms.WriteByte(0);
				ms.WriteByte(0);
				ms.WriteByte(0);
				ms.WriteByte(1);

				//Write Time
				ms.Write(p, 0, p.Length);

				//Write signature
				ms.Write(signed, 0, signed.Length);

				final = ms.ToArray();
			}

			request.Headers.Add("Signature", Convert.ToBase64String(final));
		}

		private byte[] SignData(byte[] data, AsymmetricKeyParameter privateKey)
		{
			var signer = SignerUtilities.GetSigner("SHA-256withPLAIN-ECDSA");

			signer.Init(true, privateKey);
			signer.BlockUpdate(data, 0, data.Length);

			return signer.GenerateSignature();
		}

		private long TimeStamp()
		{
			//return DateTime.UtcNow.ToFileTime();
			long unixTimestamp = (long) (DateTime.UtcNow.Subtract(new DateTime(1601, 1, 1))).TotalSeconds;
			unixTimestamp += 11644473600;

			unixTimestamp *= 10000000;

			return unixTimestamp;
		}

		private static HttpContent SetHttpContent(object content, out byte[] data)
		{
			HttpContent httpContent = null;

			if (content != null)
			{
				using (MemoryStream ms = new MemoryStream())
				{
					using (TextWriter tw = new StreamWriter(ms))
					{
						using (var jtw = new JsonTextWriter(tw) { Formatting = Formatting.Indented })
						{
							new JsonSerializer().Serialize(jtw, content);
						}
					}

					data = ms.ToArray();
				}

				httpContent = new ByteArrayContent(data);
				httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
			}
			else
			{
				data = null;
			}

			return httpContent;
		}

		private async Task<Response> Send(Request request)
		{
			var content = new FormUrlEncodedContent(request.PostData);

			HttpClient client = _httpClient;

			var res = await client.PostAsync(request.Url, content);

			string body = await res.Content.ReadAsStringAsync();

			return new Response(res.StatusCode, body);
		}

		public async Task<BedrockTokenPair> DoDeviceCodeLogin(string deviceCode)
		{
			HttpClient client = _httpClient;

			string deviceId = Guid.NewGuid().ToString();

			MsaDeviceAuthPollState? token = null;

			while (true)
			{
				token = await DevicePollState(deviceCode);
				if (token.Error != "authorization_pending")
				{
					break;
				}

				await Task.Delay(5000);
			}

			if (token == null)
			{
				//Log.Warn($"Token was null.");

				return null;
			}

			if (token.ExpiryTime == default)
			{
				token.ExpiryTime = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
			}

			return new BedrockTokenPair()
			{
				AccessToken = token.AccessToken,
				ExpiryTime = token.ExpiryTime,
				RefreshToken = token.RefreshToken,
				DeviceId = deviceId
			};
		}

		public static AsymmetricCipherKeyPair GenerateClientKey()
		{
			var generator = new ECKeyPairGenerator("ECDH");
			generator.Init(new ECKeyGenerationParameters(new DerObjectIdentifier("1.3.132.0.34"), SecureRandom.GetInstance("SHA256PRNG")));
			return generator.GenerateKeyPair();
		}

		public async Task<(bool Success, string MinecraftChain, AsymmetricCipherKeyPair minecraftKeyPair)> TryAuthenticate(string accessToken, string deviceId)
		{
			AsymmetricCipherKeyPair minecraftKeyPair = GenerateClientKey();

			var userToken = await ObtainUserToken(_httpClient, accessToken);
			SpinWait.SpinUntil(() => DateTime.UtcNow >= userToken.IssueInstant.UtcDateTime);

			var deviceToken = await ObtainDeviceToken(_httpClient, deviceId);
			SpinWait.SpinUntil(() => DateTime.UtcNow >= deviceToken.IssueInstant.UtcDateTime);

			var titleToken = await DoTitleAuth(_httpClient, deviceToken, accessToken);
			SpinWait.SpinUntil(() => DateTime.UtcNow >= titleToken.IssueInstant.UtcDateTime);

			var xsts = await DoXsts(_httpClient, deviceToken, titleToken, userToken.Token);
			SpinWait.SpinUntil(() => DateTime.UtcNow >= xsts.IssueInstant.UtcDateTime);

			var result = await RequestMinecraftChain(_httpClient, xsts, minecraftKeyPair);

			return (result.Success, result.MinecraftChain, minecraftKeyPair);
		}

		public async Task<BedrockTokenPair> RefreshAccessToken(string refreshToken)
        {
			return await RefreshAccessToken(refreshToken, ClientId);

		}

		public async Task<BedrockTokenPair> RefreshAccessToken(string refreshToken,
			string clientId,
			params string[] scopes)
		{
			if (string.IsNullOrEmpty(refreshToken))
			{
				throw new ArgumentException("The refresh token is missing.");
			}

			string scope = string.Empty;

			if (scopes.Length == 0)
			{
				scope = "service::user.auth.xboxlive.com::MBI_SSL";
			}
			else
			{
				scope = string.Join(' ', scopes);
			}

			try
			{
				AccessTokens tokens = await Get(
					$"{RefreshUri}",
					new Dictionary<string, string>
					{
						{ "client_id", clientId },
						{ "grant_type", "refresh_token" },
						{ "scope", scope },
						{ "redirect_uri", RedirectUri },
						{ "refresh_token", refreshToken }
					}).ConfigureAwait(false);

				return new BedrockTokenPair()
				{
					AccessToken = tokens.AccessToken,
					ExpiryTime = DateTime.UtcNow.AddSeconds(tokens.Expiration),
					RefreshToken = tokens.RefreshToken
				};
			}
			catch (WebException ex)
			{
				//Log.Warn(
				//	"RefreshAccessToken failed likely due to an invalid client ID or refresh token\n" + ex.ToString());
			}

			return null;
		}

		public async Task<MsaDeviceAuthConnectResponse> StartDeviceAuthConnect()
		{
			return await StartDeviceAuthConnect(ClientId);
		}

		public static bool IsMinecraftChainValid(string[] minecraftChain)
        {
			var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			foreach (var elm in minecraftChain)
			{
				var payloadJson = JWT.Payload(elm);
				var payload = JsonConvert.DeserializeObject<Dictionary<object, object>>(payloadJson);

				if(((long) payload["exp"]) < unixTime)
                {
					return false;
                }
			}

			return true;
		}

		public static bool IsAccessTokenValid(BedrockTokenPair token)
        {
			return token.ExpiryTime > DateTime.Now;
        }

		public async Task<MsaDeviceAuthConnectResponse> StartDeviceAuthConnect(string clientId, params string[] scopes)
		{
			string scope = string.Empty;

			if (scopes.Length == 0)
			{
				scope = "service::user.auth.xboxlive.com::MBI_SSL";
			}
			else
			{
				scope = string.Join(' ', scopes);
			}

			Request request = new Request($"https://login.live.com/oauth20_connect.srf?client_id={ClientId}");
			request.PostData["client_id"] = clientId;
			request.PostData["scope"] = scope;
			request.PostData["response_type"] = "device_code";

			var response = await Send(request);

			if (response.Status != HttpStatusCode.OK)
				throw new Exception("Failed to start sign in flow: non-200 status code");

			return JsonConvert.DeserializeObject<MsaDeviceAuthConnectResponse>(response.Body);
		}

		public async Task<MsaDeviceAuthPollState> DevicePollState(string deviceCode)
		{
			return await DevicePollState(deviceCode, ClientId);
		}

		public async Task<MsaDeviceAuthPollState> DevicePollState(string deviceCode, string clientId)
		{
			Request request = new Request($"https://login.live.com/oauth20_token.srf?client_id={clientId}");
			request.PostData["client_id"] = clientId;
			request.PostData["device_code"] = deviceCode;
			request.PostData["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code";

			var response = await Send(request);

			if (response.Status != HttpStatusCode.OK && (int) response.Status != 400)
				throw new Exception($"Failed to start sign in flow: non-200 status code: {response.Status}");

			return JsonConvert.DeserializeObject<MsaDeviceAuthPollState>(response.Body);
		}


		private async Task<AccessTokens> Get(string uri, Dictionary<string, string> parameters)
		{
			AccessTokens tokens = null;

			var client = _httpClient;
			var encodedContent = new FormUrlEncodedContent(parameters);
			var response = await client.PostAsync(uri, encodedContent);

			var res = await response.Content.ReadAsStringAsync();

			tokens = JsonConvert.DeserializeObject<AccessTokens>(res);

			return tokens;
		}

		private struct Request
		{
			public string Url;
			public Dictionary<string, string> PostData;

			public Request(string url)
			{
				Url = url;
				PostData = new Dictionary<string, string>();
			}
		}

		struct Response
		{
			public HttpStatusCode Status;
			public string Body;

			public Response(HttpStatusCode status, string body)
			{
				Status = status;
				Body = body;
			}
		};
	}
}
