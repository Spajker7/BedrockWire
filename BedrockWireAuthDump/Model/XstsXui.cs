using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class XstsXui
	{
		[JsonProperty("gtg")] public string Gamertag { get; set; }

		[JsonProperty("xid")] public string XUID { get; set; }

		[JsonProperty("uhs")] public string UserHash { get; set; }
	}
}
