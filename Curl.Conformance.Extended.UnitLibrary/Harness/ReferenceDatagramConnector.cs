using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>Opens each UDP channel to the UDP route on its port; a port no such route answers fails to open.</summary>
internal sealed class ReferenceDatagramConnector(IReadOnlyList<ReferenceRoute> routes, ReferenceRun run) : IDatagramConnector
{
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        ReferenceRoute? route = routes.FirstOrDefault(candidate => candidate.Port == port && candidate.Transport == ReferenceTransport.Udp);
        return ValueTask.FromResult(route is null
            ? DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, $"No reference server answers UDP port {port}")
            : DatagramOpenResult.Opened(new ReferenceDatagramChannel(route.DatagramServer!, run.Open(route), new IPEndPoint(IPAddress.Loopback, port))));
    }
}
