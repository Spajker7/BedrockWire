using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class MsaDeviceAuthPollState : BedrockTokenPair
	{
		[JsonProperty("user_id")] public string UserId;

		[JsonProperty("token_type")] public string TokenType;

		[JsonProperty("scope")] public string Scope;

		//public int interval;
		[JsonProperty("expires_in")] public int ExpiresIn;

		[JsonProperty("error")] public string Error;
	};
}
