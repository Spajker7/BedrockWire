﻿using System.Net;
using BedrockWireAuthDump;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BedrockWireAuthDumpCli
{
	class Program
	{
		public class Options
		{
			[Option('o', "output", Required = false, HelpText = "Set auth dump output file.", Default = "auth.json")]
			public string? Output { get; set; }
			[Option('t', "token", Required = false, HelpText = "Set token output file.")]
			public string? TokenOutput { get; set; }
		}

		static void Main(string[] args)
		{
			Options? options = null;
			Parser.Default.ParseArguments<Options>(args).WithParsed(o => options = o);

			AuthDump authDump = new AuthDump();

			var result = authDump.StartDeviceCode().Result;
			Console.WriteLine("Verification url: " + result.verificationUrl);
			Console.WriteLine("Code: " + result.userCode);

			var res = authDump.StartPolling().Result;

			if(res.success)
			{
				var keys = AuthDump.SerializeKeys(res.minecraftKeyPair);
				dynamic a = JObject.Parse(res.minecraftChain);
				a.privateKey = keys.priv;
				a.publicKey = keys.pub;

				using (StreamWriter writer = new StreamWriter(options.Output))
				{
					writer.Write(JsonConvert.SerializeObject(a));
				}

				if(options.TokenOutput != null)
				{
					using (StreamWriter writer = new StreamWriter(options.TokenOutput))
					{
						writer.Write(JsonConvert.SerializeObject(res.token));
					}
				}
			}
		}
	}
}