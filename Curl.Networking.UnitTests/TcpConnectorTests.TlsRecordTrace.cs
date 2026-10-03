using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesTcpFilter" /> for a direct <c>https://</c> connection: curl
/// 8.21.0's <c>[TCP] send</c> and <c>recv</c> lines for the TLS records below the TLS filter, the
/// handshake's with Schannel's 4096-byte reads and the application data's with its 103424-byte reads,
/// and no <c>[TCP] query ALPN</c> line (measured, BL-1253 Notes; ADR-0357's BL-1260 amendment).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_TracingTheTcpFilterForHttps_WritesTheHandshakeRecordsThenTheApplicationDataRecords()
    {
        // curl -s -v -k --trace-config tcp https://127.0.0.1:P/ (BL-1253 Notes); the scripted server
        // answers at once, so no would-block recv line is written.
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection(new byte[81 + 1175 + 51 + 72]) },
            new RecordExchangingTlsProvider(),
            new ManualTimeProvider())
        {
            TracesTcpFilter = true,
        };

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47195, UseTls: true) { Events = events, PoolScheme = "https" }, CancellationToken.None);
        await result.Connection!.WriteAsync(new byte[108], CancellationToken.None);
        await result.Connection.ReadAsync(new byte[72], CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "[TCP] connected on fd=3",
                "[TCP] send(len=429) -> 0, 429",
                "[TCP] recv(len=4096) -> 0, 81",
                "[TCP] recv(len=4096) -> 0, 1175",
                "[TCP] send(len=158) -> 0, 158",
                "[TCP] recv(len=4096) -> 0, 51",
                "opened",
                "[TCP] send(len=108) -> 0, 108",
                "[TCP] recv(len=103424) -> 0, 72",
            },
            events.Calls.Skip(5).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheTcpFilterForAFailedHttpsHandshake_WritesTheRecordsSentAndReturnsTheFailure()
    {
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection(new byte[7]) },
            new RecordExchangingTlsProvider { Failure = ConnectResult.Failed(CurlExitCode.SslConnectError, "refused") },
            new ManualTimeProvider())
        {
            TracesTcpFilter = true,
        };

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47195, UseTls: true) { Events = events, PoolScheme = "https" }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "[TCP] send(len=429) -> 0, 429", "[TCP] recv(len=4096) -> 0, 7" }, events.Calls.Where(line => line.Contains("(len=", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_NotTracingTheTcpFilterForHttps_HandsTheHandshakeTheDialledConnection()
    {
        var events = new CountingTransferEvents();
        var dialled = new ScriptedConnection([]);
        var provider = new FakeTlsProvider();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => dialled }, provider, new ManualTimeProvider());

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47195, UseTls: true) { Events = events, PoolScheme = "https" }, CancellationToken.None);

        Assert.AreSame(dialled, provider.ReceivedPlaintext);
    }

    [TestMethod]
    public void TlsRecordTraceFor_ADirectHttpsTargetUnderTheTcpFilter_TracesTheHandshakeWithSchannelsBuffer()
    {
        var trace = TcpConnector.TlsRecordTraceFor(new ScriptedConnection([]), tracesTcpFilter: true, new ConnectTarget("example.com", 443, UseTls: true) { PoolScheme = "https" });

        Assert.AreSame(TcpIoTraceConnection.HttpsHandshakeLines, trace!.Lines);
    }

    [TestMethod]
    [DataRow(false, "https", false, false)]
    [DataRow(true, "https", true, false)]
    [DataRow(true, "https", false, true)]
    [DataRow(true, "http", false, false)]
    [DataRow(true, null, false, false)]
    public void TlsRecordTraceFor_AnyOtherTarget_TracesNothing(bool tracesTcpFilter, string? poolScheme, bool throughProxy, bool isForwardProxy)
    {
        var target = new ConnectTarget("example.com", 443, UseTls: true)
        {
            PoolScheme = poolScheme,
            Proxy = throughProxy ? new ProxyEndpoint(ProxyKind.Socks5, "127.0.0.1", 1080, null) : null,
            IsForwardProxy = isForwardProxy,
        };

        Assert.IsNull(TcpConnector.TlsRecordTraceFor(new ScriptedConnection([]), tracesTcpFilter, target));
    }

    [TestMethod]
    public void HttpsLines_AreCurlsSchannelBuffers()
    {
        Assert.AreEqual(new TcpIoTraceLines("TCP", 4096, WritesWouldBlockReads: true), TcpIoTraceConnection.HttpsHandshakeLines);
        Assert.AreEqual(new TcpIoTraceLines("TCP", 103424, WritesWouldBlockReads: true), TcpIoTraceConnection.HttpsApplicationDataLines);
    }

    /// <summary>
    /// A TLS provider whose handshake writes and reads records of curl's measured sizes over the
    /// plaintext connection - a 429-byte hello, two reads, a 158-byte finish and a third read - and
    /// returns that connection as secured, or <see cref="Failure" /> after the hello and one read.
    /// </summary>
    private sealed class RecordExchangingTlsProvider : ITlsProvider
    {
        public ConnectResult? Failure { get; init; }

        public async ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
        {
            await plaintext.WriteAsync(new byte[429], cancellationToken);
            await plaintext.ReadAsync(new byte[81], cancellationToken);
            if (Failure is { } failure)
            {
                return failure;
            }

            await plaintext.ReadAsync(new byte[1175], cancellationToken);
            await plaintext.WriteAsync(new byte[158], cancellationToken);
            await plaintext.ReadAsync(new byte[51], cancellationToken);
            return ConnectResult.Connected(plaintext);
        }
    }
}
