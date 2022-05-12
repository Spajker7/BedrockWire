using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class XDI
	{
		[JsonProperty("did")] public string DID { get; set; }
	}
}
