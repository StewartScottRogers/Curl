using System.Text;
using Curl.Cli;
using Curl.Console;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-155 (ADR-0852): a <c>--resolve</c> entry for the DoH server's host and port answers for it,
/// as curl 8.22.0's DoH probes find the name in the DNS cache those entries were added to (measured
/// with <c>Record-CurlExchange.ps1 -DohPort</c>: both queries reached the server named
/// <c>dohsrv.test</c>), and loading them writes no <c>[DNS] added ... to cache</c> line of its own.
/// </summary>
[TestClass]
public sealed class Cgr155Tests
{
    private static readonly byte[] Forbidden = Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n");

    [TestMethod]
    public async Task CreateDnsResolver_ResolveEntryForTheDohServer_DialsTheDohServerAtTheEntrysAddress()
    {
        ScriptedConnector dohServer = new([Forbidden, Forbidden]);
        CommandLineOptions options = Parse("--resolve", "dohsrv.test:21800:192.0.2.7", "--doh-url", "http://dohsrv.test:21800/dns-query");
        IDnsResolver resolver = EngineComposition.CreateDnsResolver(
            EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), TimeProvider.System, new ScriptedTcpDialer(dohServer), allowsPlainHttpDohUrl: true);

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        Assert.IsNotEmpty(dohServer.Targets);
        Assert.IsTrue(dohServer.Targets.All(target => target.Host == "192.0.2.7" && target.Port == 21800));
    }

    [TestMethod]
    public async Task CreateDohConnector_ResolveEntries_WritesNoAddedLineOnTheDohSubTransfer()
    {
        ScriptedConnector dohServer = new([Forbidden]);
        TcpConnector connector = EngineComposition.CreateDohConnector(
            EngineTraceComponents.None, new Api.Options.Tls.EasyTlsOptions(), new ScriptedTcpDialer(dohServer), TimeProvider.System, resolveOverrides: ResolveOverrides.Parse(["dohsrv.test:21800:192.0.2.7"]));
        InfoRecordingEvents events = new();

        await connector.ConnectAsync(new ConnectTarget("dohsrv.test", 21800, false) { Events = events }, CancellationToken.None);

        Assert.AreEqual("192.0.2.7", dohServer.Targets.Single().Host);
        Assert.IsFalse(events.Lines.Any(line => line.StartsWith("[DNS] added ", StringComparison.Ordinal)));
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://foo.test:21802/x"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    /// <summary>Records the info lines; drops the rest.</summary>
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
