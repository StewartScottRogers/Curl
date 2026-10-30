using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>Checks that the late-bound connector forwards to curl's connector once bound, and fails before.</summary>
[TestClass]
public sealed class LateBoundConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_Unbound_FailsWithExitSeven()
    {
        ConnectResult result = await new LateBoundConnector().ConnectAsync(new ConnectTarget("proxy.example", 443, true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("MASQUE proxy is not reachable before curl's connector is set", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_Bound_GoesThroughTheBoundConnector()
    {
        var bound = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: proxy.example"));
        var connector = new LateBoundConnector();
        connector.Bind(bound);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("proxy.example", 443, true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(new ConnectTarget("proxy.example", 443, true), bound.Target);
    }

    /// <summary>A datagram connector a test hands over only to see it kept or set aside.</summary>
    internal sealed class UnusedDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This datagram connector is never opened.");
    }
}
