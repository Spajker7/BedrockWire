using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class AuthRequest
	{
		[JsonProperty("RelyingParty")] public string RelyingParty { get; set; }

		[JsonProperty("TokenType")] public string TokenType { get; set; }

		[JsonProperty("Properties")] public Dictionary<string, object> Properties { get; set; }
	}
}
