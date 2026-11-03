using Curl.Networking.Fakes;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <c>--no-sessionid</c> and <c>--ssl-allow-beast</c> in the hand-built client (ADR-0151, BL-713):
/// the run's session cache is offered by default and not at all under <c>--no-sessionid</c>, and
/// the TLS 1.0 CBC empty-fragment split is on by default and off under <c>--ssl-allow-beast</c>.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    private const string ResumedHost = "example.com";

    private const uint CachedTicketAgeAdd = 5;

    private static readonly byte[] CachedTicket = [7, 1, 2];

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithASessionInTheRunsCache_OffersItToResume()
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13);
        var sessions = CacheHoldingASessionFor(options);
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MinimumVersion: Tls13");
        Diagnostics.Arrange("cache", "holds one unexpired TLS 1.3 session for the peer");

        byte[] clientHello;
        using (Diagnostics.Phase("TLS handshake"))
        {
            clientHello = await CaptureClientHelloWithSessionsAsync(options, sessions);
        }

        var hello = DecodeClientHello(clientHello);
        Diagnostics.Bytes("ClientHello record", clientHello);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(hello)));
        Diagnostics.Assert("offers pre_shared_key", true, ExtensionTypes(hello).Contains(TlsExtensionType.PreSharedKey));
        CollectionAssert.Contains(ExtensionTypes(hello), TlsExtensionType.PreSharedKey);
        Diagnostics.Assert("session left in the cache", true, sessions.Take(PeerKeyOf(options)) is null);
        Assert.IsNull(sessions.Take(PeerKeyOf(options)), "the offered session was taken out of the cache");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithNoSessionId_OffersNoSessionAndLeavesTheCacheAlone()
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13, NoSessionId: true);
        var sessions = CacheHoldingASessionFor(options);
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MinimumVersion: Tls13, NoSessionId: true");
        Diagnostics.Arrange("cache", "holds one unexpired TLS 1.3 session for the peer");

        byte[] clientHello;
        using (Diagnostics.Phase("TLS handshake"))
        {
            clientHello = await CaptureClientHelloWithSessionsAsync(options, sessions);
        }

        var hello = DecodeClientHello(clientHello);
        Diagnostics.Bytes("ClientHello record", clientHello);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(hello)));
        Diagnostics.Assert("offers pre_shared_key", false, ExtensionTypes(hello).Contains(TlsExtensionType.PreSharedKey));
        CollectionAssert.DoesNotContain(ExtensionTypes(hello), TlsExtensionType.PreSharedKey);
        var keptSession = sessions.Take(PeerKeyOf(options));
        Diagnostics.Assert("kept session ticket", Convert.ToHexString(CachedTicket), keptSession is null ? "none" : Convert.ToHexString(keptSession.Ticket));
        CollectionAssert.AreEqual(CachedTicket, keptSession?.Ticket, "--no-sessionid neither takes nor replaces the cached session");
        Assert.AreEqual(CachedTicketAgeAdd, keptSession!.TicketAgeAdd, "the cache still holds the session it was given");
        Assert.IsNull(sessions.Take(PeerKeyOf(options)), "the handshake added no session beside the cached one");
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void InsertsEmptyFragment_IsOnUnlessSslAllowBeast(bool allowBeast, bool expected)
    {
        Diagnostics.Arrange("AllowBeast", allowBeast);
        Diagnostics.Arrange("expected empty fragment", expected);

        var actual = HandBuiltTlsProvider.InsertsEmptyFragment(new TlsClientOptions(AllowBeast: allowBeast));

        Diagnostics.Act("inserts empty fragment", actual);
        Diagnostics.Assert("inserts empty fragment", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private static string PeerKeyOf(TlsClientOptions options) =>
        TlsSessionCache.PeerKey(ResumedHost, ServerEndPoint.Port, options);

    // A cache holding one unexpired TLS 1.3 session for the test's peer, as a first transfer leaves it.
    private static TlsSessionCache CacheHoldingASessionFor(TlsClientOptions options)
    {
        var sessions = new TlsSessionCache(TimeProvider.System);
        TlsSessionRecord session = new(0x0304, 0x1301, new byte[32], new byte[32], CachedTicket, 7200, CachedTicketAgeAdd, 0, TimeProvider.System.GetUtcNow())
        {
            ServerName = ResumedHost,
            ApplicationProtocol = "http/1.1",
            Group = 0x001D,
        };
        sessions.Track(PeerKeyOf(options), () => [session]);
        _ = sessions.Export();
        return sessions;
    }

    // Runs a provider over the cache against a server that records the first record it receives and closes.
    private static async Task<byte[]> CaptureClientHelloWithSessionsAsync(TlsClientOptions options, TlsSessionCache sessions)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var header = new byte[5];
            await server.ReadExactlyAsync(header);
            var body = new byte[(header[3] << 8) | header[4]];
            await server.ReadExactlyAsync(body);
            await server.DisposeAsync();
            return (byte[])[.. header, .. body];
        });
        var provider = new HandBuiltTlsProvider(options, OpenSslBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance, sessions);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), ResumedHost, new RecordingTransferEvents(), false, Http11, CancellationToken.None);

        Assert.AreNotEqual(Protocol.Abstractions.CurlExitCode.Ok, result.ExitCode);
        return await serverTask;
    }
}
