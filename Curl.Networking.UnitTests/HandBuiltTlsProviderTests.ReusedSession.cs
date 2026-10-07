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
        ArrangeResumption("OpenSSL", "Insecure: true, AllowEarlyData: true", "http/1.1", seedSession: true);

        RecordingTransferEvents events;
        using (Diagnostics.Phase("TLS handshake"))
        {
            events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true), OpenSslBuild, "http/1.1");
        }

        var lines = ReusedSessionLines(events);
        Diagnostics.Act("reusing session lines", string.Join(" | ", lines));
        Diagnostics.Assert("reusing session lines", "SSL reusing session with ALPN 'http/1.1'", string.Join(" | ", lines));
        CollectionAssert.AreEqual(new[] { "SSL reusing session with ALPN 'http/1.1'" }, ReusedSessionLines(events));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_OfferingASessionWithoutAlpn_WritesADashForTheAlpn()
    {
        ArrangeResumption("OpenSSL", "Insecure: true, AllowEarlyData: true", null, seedSession: true);

        RecordingTransferEvents events;
        using (Diagnostics.Phase("TLS handshake"))
        {
            events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true), OpenSslBuild, null);
        }

        var lines = ReusedSessionLines(events);
        Diagnostics.Act("reusing session lines", string.Join(" | ", lines));
        Diagnostics.Assert("reusing session lines", "SSL reusing session with ALPN '-'", string.Join(" | ", lines));
        CollectionAssert.AreEqual(new[] { "SSL reusing session with ALPN '-'" }, ReusedSessionLines(events));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithoutACachedSession_WritesNoReusingSessionLine()
    {
        ArrangeResumption("OpenSSL", "Insecure: true, AllowEarlyData: true", "http/1.1", seedSession: false);

        RecordingTransferEvents events;
        using (Diagnostics.Phase("TLS handshake"))
        {
            events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true), OpenSslBuild, "http/1.1", seedSession: false);
        }

        Diagnostics.Act("reusing session lines", ReusedSessionLines(events).Length);
        Diagnostics.Assert("reusing session line count", 0, ReusedSessionLines(events).Length);
        Assert.IsEmpty(ReusedSessionLines(events));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_UnderNoSessionId_WritesNoReusingSessionLine()
    {
        ArrangeResumption("OpenSSL", "Insecure: true, AllowEarlyData: true, NoSessionId: true", "http/1.1", seedSession: true);

        RecordingTransferEvents events;
        using (Diagnostics.Phase("TLS handshake"))
        {
            events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true, NoSessionId: true), OpenSslBuild, "http/1.1");
        }

        Diagnostics.Act("reusing session lines", ReusedSessionLines(events).Length);
        Diagnostics.Assert("reusing session line count", 0, ReusedSessionLines(events).Length);
        Assert.IsEmpty(ReusedSessionLines(events));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_OfferingASessionAsTheSchannelBuild_WritesNoReusingSessionLine()
    {
        ArrangeResumption("Schannel", "Insecure: true, AllowEarlyData: true", "http/1.1", seedSession: true);

        RecordingTransferEvents events;
        using (Diagnostics.Phase("TLS handshake"))
        {
            events = await ResumeWithTestServerAsync(new TlsClientOptions(Insecure: true, AllowEarlyData: true), SchannelBuild, "http/1.1");
        }

        Diagnostics.Act("reusing session lines", ReusedSessionLines(events).Length);
        Diagnostics.Assert("reusing session line count", 0, ReusedSessionLines(events).Length);
        Assert.IsEmpty(ReusedSessionLines(events));
    }

    private void ArrangeResumption(string build, string options, string? sessionApplicationProtocol, bool seedSession)
    {
        Diagnostics.Arrange("build", build);
        Diagnostics.Arrange("options", options);
        Diagnostics.Arrange("server TLS protocol", "TLS 1.3, resumes the seeded ticket 05 05 05 05");
        Diagnostics.Arrange("session ALPN", sessionApplicationProtocol ?? "(none)");
        Diagnostics.Arrange("session seeded in the cache", seedSession);
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
