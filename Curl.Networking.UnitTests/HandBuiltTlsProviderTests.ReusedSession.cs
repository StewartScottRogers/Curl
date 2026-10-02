using System.Security.Cryptography;

using Curl.Networking.Fakes;
using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The OpenSSL build's <c>-v</c> line for a session the provider offers from the run's cache (BL-1142),
/// as measured with curl 8.18.0 and OpenSSL 3.5.5 against <c>openssl s_server -www</c>.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    private const string ReusedSessionLinePrefix = "SSL reusing session";

    [TestMethod]
    public async Task AuthenticateAsClientAsync_OfferingASessionWithAlpn_WritesTheReusingSessionLineOnce()
    {
        var events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true), OpenSslBuild, "http/1.1");

        CollectionAssert.AreEqual(new[] { "SSL reusing session with ALPN 'http/1.1'" }, ReusedSessionLines(events));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_OfferingASessionWithoutAlpn_WritesADashForTheAlpn()
    {
        var events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true), OpenSslBuild, null);

        CollectionAssert.AreEqual(new[] { "SSL reusing session with ALPN '-'" }, ReusedSessionLines(events));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithoutACachedSession_WritesNoReusingSessionLine()
    {
        var events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true), OpenSslBuild, "http/1.1", seedSession: false);

        Assert.IsEmpty(ReusedSessionLines(events));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_UnderNoSessionId_WritesNoReusingSessionLine()
    {
        var events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true, NoSessionId: true), OpenSslBuild, "http/1.1");

        Assert.IsEmpty(ReusedSessionLines(events));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_OfferingASessionAsTheSchannelBuild_WritesNoReusingSessionLine()
    {
        var events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true), SchannelBuild, "http/1.1");

        Assert.IsEmpty(ReusedSessionLines(events));
    }

    private static string[] ReusedSessionLines(RecordingTransferEvents events) =>
        [.. events.Info.Where(line => line.StartsWith(ReusedSessionLinePrefix, StringComparison.Ordinal))];

    // Handshakes with a server that resumes the seeded ticket, from a cache holding it (or empty),
    // and returns the client's lines.
    private static async Task<RecordingTransferEvents> ResumeWithTestServerAsync(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        string? sessionApplicationProtocol,
        bool seedSession = true)
    {
        using var pki = new OcspTestPki();
        byte[] ticket = [5, 5, 5, 5];
        byte[] preSharedKey = RandomNumberGenerator.GetBytes(32);
        var sessions = seedSession
            ? SessionsHolding(EarlySession(preSharedKey, ticket, sessionApplicationProtocol, maxEarlyDataSize: 0))
            : new TlsSessionCache(TimeProvider.System);
        var server = new Tls13TestServer(pki.LeafCredential) { Tickets = new() { [Convert.ToHexString(ticket)] = preSharedKey } };
        var provider = new HandBuiltTlsProvider(
            options, matchesSchannelBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance, sessions);
        var events = new RecordingTransferEvents();

        var (result, _) = await HandshakeWithTestServerAsync(provider, server, events);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await result.Connection!.DisposeAsync();
        return events;
    }
}
