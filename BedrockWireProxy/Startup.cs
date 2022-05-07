using System.Net;
using CommandLine;
using MiNET;
using MiNET.Net.RakNet;
using Newtonsoft.Json;

namespace BedrockWireProxy
{
	class Startup
	{
		public class Options
		{
			[Option('p', "port", Required = false, HelpText = "Set proxy's local listen port.", Default = 19135)]
			public int ListenPort { get; set; }
			[Option('r', "remote", Required = false, HelpText = "Set remote server address.", Default = "127.0.0.1:19132")]
			public string? RemoteAddress { get; set; }
			[Option('o', "output", Required = false, HelpText = "Set output <stdout/file path>", Default = "out.bw")]
			public string? Output { get; set; }
			[Option('f', "format", Required = false, HelpText = "Set output format [binary/human]", Default = "binary")]
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
			AuthData authData = new AuthData(JsonConvert.DeserializeObject<AuthDataSerialized>(authDataJson));

			IPEndPoint remoteServer = IPEndPoint.Parse(options.RemoteAddress);

			PacketWriter packetWriter = new PacketWriter(options.Output, options.Format);

			RakConnection connection = new RakConnection(new IPEndPoint(IPAddress.Any, options.ListenPort), new GreyListManager(), new MotdProvider());
			connection.CustomMessageHandlerFactory = session => new ProxyServerMessageHandler(session, remoteServer, authData, packetWriter);

			ConnectionInfo connectionInfo = connection.ConnectionInfo;
			connectionInfo.MaxNumberOfPlayers = 1;
			connectionInfo.MaxNumberOfConcurrentConnects = 1;

			connection.Start();
			
			CancellationTokenSource tokenSource = new CancellationTokenSource();

			Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs args) { 
				args.Cancel = true;
				tokenSource.Cancel(true);
			};

			packetWriter.StartWriting(tokenSource.Token);


			connection.Stop();
		}
	}
}