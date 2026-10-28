using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins that an unusable <c>--doh-url</c>'s <see cref="UnusableDohUrlResolver" /> makes a failed look-up write
/// the <c>-v</c> line <c>Could not resolve host: &lt;host&gt;</c> once, as curl 8.21.0 does (measured, BL-3916).
/// </summary>
[TestClass]
public sealed class UnusableDohUrlResolverVerboseTests
{
    [TestMethod]
    public async Task ConnectAsync_WhenTheUnusableDohUrlResolverAnswersNothing_WritesCurlsTwoLines()
    {
        var events = new InfoRecordingEvents();
        var connector = new TcpConnector(new UnusableDohUrlResolver(), new TcpDialer(), new PassThroughTlsProvider(), TimeProvider.System);

        var result = await connector.ConnectAsync(new ConnectTarget("foo.test", 21802, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "Could not resolve host: foo.test", "Could not resolve: foo.test:21802" }, events.Lines);
    }

    private sealed class InfoRecordingEvents : ITransferEvents
    {
        public List<string> Lines { get; } = [];

        public void ReportInfo(string message) => Lines.Add(message);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
