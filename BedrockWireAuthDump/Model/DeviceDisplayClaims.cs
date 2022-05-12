using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class DeviceDisplayClaims
	{
		[JsonProperty("xdi")] public XDI Xdi { get; set; }
	}
}
