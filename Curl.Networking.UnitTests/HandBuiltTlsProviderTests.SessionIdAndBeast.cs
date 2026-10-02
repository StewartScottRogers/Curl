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

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithASessionInTheRunsCache_OffersItToResume()
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13);
        var sessions = CacheHoldingASessionFor(options);

        var hello = DecodeClientHello(await CaptureClientHelloWithSessionsAsync(options, sessions));

        CollectionAssert.Contains(ExtensionTypes(hello), TlsExtensionType.PreSharedKey);
        Assert.IsNull(sessions.Take(PeerKeyOf(options)), "the offered session was taken out of the cache");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithNoSessionId_OffersNoSessionAndLeavesTheCacheAlone()
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13, NoSessionId: true);
        var sessions = CacheHoldingASessionFor(options);

        var hello = DecodeClientHello(await CaptureClientHelloWithSessionsAsync(options, sessions));

        CollectionAssert.DoesNotContain(ExtensionTypes(hello), TlsExtensionType.PreSharedKey);
        Assert.IsNotNull(sessions.Take(PeerKeyOf(options)), "--no-sessionid neither takes nor keeps a session");
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void InsertsEmptyFragment_IsOnUnlessSslAllowBeast(bool allowBeast, bool expected) =>
        Assert.AreEqual(expected, HandBuiltTlsProvider.InsertsEmptyFragment(new TlsClientOptions(AllowBeast: allowBeast)));

    private static string PeerKeyOf(TlsClientOptions options) =>
        TlsSessionCache.PeerKey(ResumedHost, ServerEndPoint.Port, options);

    // A cache holding one unexpired TLS 1.3 session for the test's peer, as a first transfer leaves it.
    private static TlsSessionCache CacheHoldingASessionFor(TlsClientOptions options)
    {
        var sessions = new TlsSessionCache(TimeProvider.System);
        TlsSessionRecord session = new(0x0304, 0x1301, new byte[32], new byte[32], [7, 1, 2], 7200, 5, 0, TimeProvider.System.GetUtcNow())
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
