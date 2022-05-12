using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class MsaDeviceAuthConnectResponse : IDeviceAuthConnectResponse
	{
		/// <inheritdoc />
		[JsonProperty("user_code")]
		public string UserCode { get; set; }

		/// <inheritdoc />
		[JsonProperty("device_code")]
		public string DeviceCode { get; set; }

		/// <inheritdoc />
		[JsonProperty("verification_uri")]
		public string VerificationUrl { get; set; }

		/// <inheritdoc />
		[JsonProperty("interval")]
		public int Interval { get; set; }

		/// <inheritdoc />
		[JsonProperty("expires_in")]
		public int ExpiresIn { get; set; }
	};
}
