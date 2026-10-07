using System.Net;

using Curl.Networking.Fakes;
using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>The provider's use of the run's <c>--ssl-sessions</c> cache (ADR-0319, BL-710).</summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    public void Constructor_WithNeitherSessionCacheNorEchLookup_IsAProviderThatKeepsNoSessions()
    {
        Diagnostics.Arrange("session cache", "(none)");
        Diagnostics.Arrange("ECH lookup", "(none)");

        var warnings = new HandBuiltTlsProvider(new TlsClientOptions(), TimeProvider.System, null, null).Warnings;

        Diagnostics.Act("warnings", warnings.Count);
        Diagnostics.Assert("warnings", 0, warnings.Count);
        Assert.IsEmpty(warnings);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithSessionCache_TracksTheConnectionsTickets()
    {
        using var pki = new OcspTestPki();
        var sessions = new TlsSessionCache(TimeProvider.System);
        var provider = new HandBuiltTlsProvider(
            new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13), OpenSslBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance, sessions);

        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MinimumVersion: Tls13");
        Diagnostics.Arrange("session cache", "empty, shared with the provider");
        Diagnostics.Arrange("server", "TLS 1.3 sends no ticket");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithTestServerAsync(provider, new Tls13TestServer(pki.LeafCredential));
        }

        ActConnectResult(result);
        var exported = sessions.Export();
        Diagnostics.Act("exported sessions length", exported.Length);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Diagnostics.Assert("exported sessions", string.Empty, exported);
        Assert.AreEqual(string.Empty, sessions.Export(), "the test server sends no ticket");
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public void PortOf_TakesTheAddressesPortOrHttps()
    {
        Diagnostics.Arrange("address with a port", "loopback:8443");
        Diagnostics.Arrange("address", "null");

        var portOfAddress = HandBuiltTlsProvider.PortOf(new IPEndPoint(IPAddress.Loopback, 8443));
        var portOfNull = HandBuiltTlsProvider.PortOf(null);

        Diagnostics.Act("port of the address", portOfAddress);
        Diagnostics.Act("port of null", portOfNull);
        Diagnostics.Assert("port of the address", 8443, portOfAddress);
        Assert.AreEqual(8443, HandBuiltTlsProvider.PortOf(new IPEndPoint(IPAddress.Loopback, 8443)));
        Diagnostics.Assert("port of null", 443, portOfNull);
        Assert.AreEqual(443, HandBuiltTlsProvider.PortOf(null));
    }

    [TestMethod]
    public void ReceivedSessionsOf_AStreamThatIsNotTls13_IsEmpty()
    {
        Diagnostics.Arrange("stream", "an empty MemoryStream, not a TLS 1.3 stream");

        var sessions = HandBuiltTlsProvider.ReceivedSessionsOf(new MemoryStream());

        Diagnostics.Act("received sessions", sessions.Count);
        Diagnostics.Assert("received sessions", 0, sessions.Count);
        Assert.IsEmpty(sessions);
    }
}
