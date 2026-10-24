using System.Net;
using System.Net.Sockets;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.IntegrationTests.CliProtocols.Host;

/// <summary>Opens passive data listeners on free loopback ports, as proto_servers.py's <c>bind(("127.0.0.1", 0))</c> did.</summary>
internal sealed class LoopbackPassiveDataListenerOpener : IPassiveDataListenerOpener
{
    /// <inheritdoc />
    public IPassiveDataListener Open() => new LoopbackListener();

    private sealed class LoopbackListener : IPassiveDataListener
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public LoopbackListener() => _listener.Start();

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public async Task<Stream> AcceptAsync(CancellationToken cancellationToken)
        {
            var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            return client.GetStream();
        }

        public void Dispose() => _listener.Stop();
    }
}
