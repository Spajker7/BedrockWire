using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	[JsonObject(MemberSerialization.OptIn)]
	public class AccessTokens
	{
		[JsonProperty("expires_in")] public int Expiration { get; set; }

		[JsonProperty("access_token")] public string AccessToken { get; set; }

		[JsonProperty("refresh_token")] public string RefreshToken { get; set; }
	}
}
