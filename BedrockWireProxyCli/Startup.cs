using System.Diagnostics;
using System.Net;
using BedrockWireProxy;
using CommandLine;
using MiNET;
using MiNET.Net.RakNet;
using Newtonsoft.Json;

namespace BedrockWireProxyCli
{
	class Startup
	{
		public class Options
		{
			[Option('p', "port", Required = false, HelpText = "Set proxy's local listen port.", Default = 19135)]
			public int ListenPort { get; set; }
			[Option('r', "remote", Required = false, HelpText = "Set remote server address.", Default = "127.0.0.1:19132")]
			public string? RemoteAddress { get; set; }
			[Option('o', "output", Required = false, HelpText = "Set output <stdout/file path>", Default = "stdout")]
			public string? Output { get; set; }
			[Option('f', "format", Required = false, HelpText = "Set output format <binary>", Default = "binary")]
			public string? Format { get; set; }
			[Option('a', "auth", Required = false, HelpText = "Set auth")]
			public string? Auth { get; set; }
			[Option('l', "authfile", Required = false, HelpText = "Set auth file.", Default = "auth.json")]
			public string? AuthFile { get; set; }
		}

		static void Main(string[] args)
		{
			Options? options = null;
			Parser.Default.ParseArguments<Options>(args).WithParsed(o => options = o);

			string authDataJson = options.Auth;
			if(authDataJson == null)
			{
				using(StreamReader sr = new StreamReader(options.AuthFile))
				{
					authDataJson = sr.ReadToEnd();
				}
			}

			IPEndPoint remoteServer = IPEndPoint.Parse(options.RemoteAddress);
			AuthData authData = JsonConvert.DeserializeObject<AuthDataSerialized>(authDataJson).ToAuthData();
			PacketWriter packetWriter = new PacketWriter(options.Output, options.Format);

			BedrockWireProxy.BedrockWireProxy proxy = new BedrockWireProxy.BedrockWireProxy(authData, options.ListenPort, remoteServer, packetWriter);
			
			CancellationTokenSource tokenSource = new CancellationTokenSource();
			Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs args) { 
				args.Cancel = true;
				tokenSource.Cancel(true);
			};

			proxy.Start();

			packetWriter.StartWriting(tokenSource.Token);

			proxy.Stop();
		}
	}
}