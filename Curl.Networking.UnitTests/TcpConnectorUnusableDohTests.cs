using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="TcpConnector" /> writes <c>Could not resolve host: &lt;host&gt;</c> once when an
/// <see cref="IDohUrlDnsResolver" /> answers nothing, as curl 8.21.0 does for an unusable <c>--doh-url</c>,
/// and twice for any other resolver (measured, BL-3916).
/// </summary>
[TestClass]
public sealed class TcpConnectorUnusableDohTests
{
    [TestMethod]
    public async Task ConnectAsync_WhenADohUrlResolverAnswersNothing_WritesCouldNotResolveHostOnce()
    {
        var lines = await FailedInfoAsync(new EmptyDohUrlResolver());

        CollectionAssert.AreEqual(new[] { "Could not resolve host: foo.test", "Could not resolve: foo.test:21802" }, lines);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAShuffledDohUrlResolverAnswersNothing_WritesCouldNotResolveHostOnce()
    {
        var lines = await FailedInfoAsync(ShufflingDnsResolver.Over(new EmptyDohUrlResolver(), shuffleAddresses: true));

        Assert.AreEqual(1, lines.Count(line => line == "Could not resolve host: foo.test"));
    }

    private static async Task<string[]> FailedInfoAsync(IDnsResolver resolver)
    {
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        var result = await connector.ConnectAsync(new ConnectTarget("foo.test", 21802, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        return [.. events.Info];
    }

    private sealed class EmptyDohUrlResolver : IDohUrlDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([]);
    }
}
