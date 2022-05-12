using MiNET;
using MiNET.Net.RakNet;
using System.Net;

namespace BedrockWireProxy
{
    public class BedrockWireProxy
    {
        private readonly AuthData auth;
        private readonly int proxyPort;
        private readonly IPEndPoint remoteServer;
        private readonly IPacketWriter packetWriter;
        private readonly MotdProvider motdProvider;
        private RakConnection? connection;

        public BedrockWireProxy(AuthData auth, int proxyPort, IPEndPoint remoteServer, IPacketWriter packetWriter) : this(auth, proxyPort, remoteServer, packetWriter, new MotdProvider()
        {
            Motd = "BedrockWire Proxy",
            SecondLine = "BedrockWire"
        }){}

        public BedrockWireProxy(AuthData auth, int proxyPort, IPEndPoint remoteServer, IPacketWriter packetWriter, MotdProvider motdProvider)
        {
            this.auth = auth;
            this.proxyPort = proxyPort;
            this.remoteServer = remoteServer;
            this.packetWriter = packetWriter;
            this.motdProvider = motdProvider;
        }

        public void Start()
        {
            connection = new RakConnection(new IPEndPoint(IPAddress.Any, proxyPort), new GreyListManager(), motdProvider);
            connection.CustomMessageHandlerFactory = session => new ProxyServerMessageHandler(session, remoteServer, auth, packetWriter);

			ConnectionInfo connectionInfo = connection.ConnectionInfo;
			connectionInfo.MaxNumberOfPlayers = 1;
			connectionInfo.MaxNumberOfConcurrentConnects = 1;

            connection.Start();
		}

        public void Stop()
        {
            connection?.Stop();
        }
    }
}