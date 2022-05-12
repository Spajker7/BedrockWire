using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class MinecraftTokenResponse
	{
		[JsonProperty("username")] public string Username { get; set; }

		[JsonProperty("roles")] public string[] Roles { get; set; }

		[JsonProperty("access_token")] public string AccessToken { get; set; }

		[JsonProperty("token_type")] public string TokenType { get; set; }

		[JsonProperty("expires_in")] public long ExpiresIn { get; set; }
	}
}
