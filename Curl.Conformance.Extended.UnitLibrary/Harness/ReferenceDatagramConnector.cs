using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// Opens each UDP channel to the UDP route on its port; a host under RFC 6761's reserved <c>.invalid</c> name fails
/// to resolve, as the harness's resolver fails it over TCP (BL-4652), and a port no such route answers fails to open.
/// </summary>
internal sealed class ReferenceDatagramConnector(IReadOnlyList<ReferenceRoute> routes, ReferenceRun run) : IDatagramConnector
{
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (IsUnderInvalid(host))
        {
            return ValueTask.FromResult(DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, $"Could not resolve host: {host}"));
        }

        ReferenceRoute? route = routes.FirstOrDefault(candidate => candidate.Port == port && candidate.Transport == ReferenceTransport.Udp);
        return ValueTask.FromResult(route is null
            ? DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, $"No reference server answers UDP port {port}")
            : DatagramOpenResult.Opened(new ReferenceDatagramChannel(route.DatagramServer!, run.Open(route), new IPEndPoint(IPAddress.Loopback, port))));
    }

    private static bool IsUnderInvalid(string host) =>
        ("." + host.TrimEnd('.')).EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);
}
