using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class BedrockTokenPair
	{
		[JsonProperty("access_token")] public string AccessToken;

		[JsonProperty("refresh_token")] public string RefreshToken;

		[JsonProperty("expiry_time")] public DateTime ExpiryTime;

		[JsonProperty("device_id")] public string DeviceId;
	}
}
