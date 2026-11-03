using Curl.Conformance.Extended.Harness;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.UnitTests.Harness;

[TestClass]
public sealed class ReferenceDatagramConnectorTests
{
    // A host under .invalid never resolves (RFC 6761), so a UDP transfer ends with curl's exit 6 as a TCP one does (BL-4652).
    [TestMethod]
    [DataRow("nohost.invalid")]
    [DataRow("NoHost.INVALID.")]
    [DataRow("invalid")]
    public async Task OpenAsync_HostUnderInvalid_FailsWithCouldntResolveHost(string host)
    {
        using ReferenceRun run = new();
        ReferenceDatagramConnector connector = new([], run);

        DatagramOpenResult result = await connector.OpenAsync(host, 5683, CancellationToken.None);

        Assert.IsNull(result.Channel);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual($"Could not resolve host: {host}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenAsync_ResolvableHostNoUdpRoute_FailsWithCouldntConnect()
    {
        using ReferenceRun run = new();
        ReferenceDatagramConnector connector = new([], run);

        DatagramOpenResult result = await connector.OpenAsync("notinvalid", 5683, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("No reference server answers UDP port 5683", result.ErrorMessage);
    }
}
