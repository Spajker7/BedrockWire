using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public interface IDeviceAuthConnectResponse
	{
		string UserCode { get; }
		string DeviceCode { get; }
		string VerificationUrl { get; }
		int Interval { get; }
		int ExpiresIn { get; }
	}
}
