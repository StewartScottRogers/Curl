using System.Net.Security;
using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the ALPN offer and <c>--no-alpn</c> (<see cref="TlsClientOptions.UseAlpn" />), measured
/// against curl 8.21.0's Schannel build (BL-490): an HTTPS transfer offers <c>http/1.1</c> and
/// <c>-v</c> prints <c>ALPN: curl offers http/1.1</c>; with <c>--no-alpn</c> the handshake
/// carries no ALPN extension and <c>-v</c> prints no <c>ALPN:</c> line.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    private static readonly string[] Http11 = ["http/1.1"];

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithApplicationProtocols_OffersThemThroughAlpn()
    {
        var handshakeOptions = await CaptureHandshakeOptionsAsync(new TlsClientOptions(Insecure: true), Http11);

        Assert.AreEqual(SslApplicationProtocol.Http11, Assert.ContainsSingle(handshakeOptions.ApplicationProtocols!));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithApplicationProtocolsAndNoAlpn_SendsNoAlpnExtension()
    {
        var handshakeOptions = await CaptureHandshakeOptionsAsync(new TlsClientOptions(Insecure: true, UseAlpn: false), Http11);

        Assert.IsNull(handshakeOptions.ApplicationProtocols);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithApplicationProtocols_ReportsTheOfferAndThatTheServerSelectedNone()
    {
        var events = new RecordingTransferEvents();

        var result = await AlpnReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events, Http11);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        CollectionAssert.AreEqual(Http11, handshake.OfferedApplicationProtocols.ToArray());
        Assert.IsNull(handshake.NegotiatedApplicationProtocol);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithApplicationProtocolsAndNoAlpn_ReportsNoOffer()
    {
        var events = new RecordingTransferEvents();

        var result = await AlpnReportingHandshakeAsync(new TlsClientOptions(Insecure: true, UseAlpn: false), events, Http11);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsEmpty(Assert.ContainsSingle(events.Handshakes).OfferedApplicationProtocols);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithNullApplicationProtocols_ThrowsArgumentNullException()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true));

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await provider.AuthenticateAsClientAsync(
                new FakeConnection(), CertificateHost, new RecordingTransferEvents(), isProxy: false, null!, CancellationToken.None));

        Assert.AreEqual("applicationProtocols", exception.ParamName);
    }

    [TestMethod]
    public void NegotiatedApplicationProtocol_WhenTheServerSelectedNone_IsNull()
    {
        Assert.IsNull(SslStreamTlsProvider.NegotiatedApplicationProtocol(default));
    }

    [TestMethod]
    public void NegotiatedApplicationProtocol_WhenTheServerSelectedHttp11_NamesIt()
    {
        Assert.AreEqual("http/1.1", SslStreamTlsProvider.NegotiatedApplicationProtocol(SslApplicationProtocol.Http11));
    }

    [TestMethod]
    public void ToSslApplicationProtocols_WithNone_IsNullSoNoAlpnExtensionIsSent()
    {
        Assert.IsNull(SslStreamTlsProvider.ToSslApplicationProtocols([]));
    }

    [TestMethod]
    public void ToSslApplicationProtocols_WithSeveral_KeepsTheirOrder()
    {
        CollectionAssert.AreEqual(
            new[] { SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 },
            SslStreamTlsProvider.ToSslApplicationProtocols(["h2", "http/1.1"]));
    }

    private static async Task<SslClientAuthenticationOptions> CaptureHandshakeOptionsAsync(
        TlsClientOptions options,
        IReadOnlyList<string> applicationProtocols)
    {
        SslClientAuthenticationOptions? handshakeOptions = null;
        var provider = new SslStreamTlsProvider(options, SchannelBuild)
        {
            AuthenticateSslStreamAsClientAsync = (_, authenticationOptions, _) =>
            {
                handshakeOptions = authenticationOptions;
                throw new AuthenticationException("The test stops before the handshake.");
            },
        };
        var (client, _) = InMemoryDuplexStream.CreatePair();

        await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint),
            CertificateHost,
            new RecordingTransferEvents(),
            isProxy: false,
            applicationProtocols,
            CancellationToken.None);

        return handshakeOptions!;
    }

    private static async Task<ConnectResult> AlpnReportingHandshakeAsync(
        TlsClientOptions options,
        RecordingTransferEvents events,
        IReadOnlyList<string> applicationProtocols)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.None);
        var provider = new SslStreamTlsProvider(options, SchannelBuild);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: false, applicationProtocols, CancellationToken.None);

        if (result.Connection is null)
        {
            await IgnoreFailureAsync(serverTask);
        }

        return result;
    }
}
