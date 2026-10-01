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
    public void Constructor_WithoutSessionCache_Throws() =>
        Assert.AreEqual("sessions", Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(new TlsClientOptions(), TimeProvider.System, null!)).ParamName);

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithSessionCache_TracksTheConnectionsTickets()
    {
        using var pki = new OcspTestPki();
        var sessions = new TlsSessionCache(TimeProvider.System);
        var provider = new HandBuiltTlsProvider(
            new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13), OpenSslBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance, sessions);

        var (result, _) = await HandshakeWithTestServerAsync(provider, new Tls13TestServer(pki.LeafCredential));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(string.Empty, sessions.Export(), "the test server sends no ticket");
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public void PortOf_TakesTheAddressesPortOrHttps()
    {
        Assert.AreEqual(8443, HandBuiltTlsProvider.PortOf(new IPEndPoint(IPAddress.Loopback, 8443)));
        Assert.AreEqual(443, HandBuiltTlsProvider.PortOf(null));
    }

    [TestMethod]
    public void ReceivedSessionsOf_AStreamThatIsNotTls13_IsEmpty() =>
        Assert.IsEmpty(HandBuiltTlsProvider.ReceivedSessionsOf(new MemoryStream()));
}
