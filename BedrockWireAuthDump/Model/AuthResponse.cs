using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class AuthResponse<TClaims>
	{
		[JsonProperty("IssueInstant")] public DateTimeOffset IssueInstant { get; set; }

		[JsonProperty("NotAfter")] public DateTimeOffset NotAfter { get; set; }

		[JsonProperty("Token")] public string Token { get; set; }

		[JsonProperty("DisplayClaims")] public TClaims DisplayClaims { get; set; }
	}
}
