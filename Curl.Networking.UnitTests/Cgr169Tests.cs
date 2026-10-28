using System.Net;
using System.Net.Security;
using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-169 of the third-party gap report (BL-3698): curl's OpenSSL build offers
/// <c>post_handshake_auth</c> and answers a TLS 1.3 post-handshake CertificateRequest with an
/// empty Certificate when it has no <c>--cert</c>. <c>SslStream</c> on Linux offers the
/// extension only when it can choose a certificate, so the OpenSSL build always gives it a
/// selection callback, which chooses none without <c>--cert</c> (ADR-0949). The Schannel
/// build gives none, as before (BL-254).
/// </summary>
[TestClass]
public sealed class Cgr169Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task AuthenticateAsClientAsync_OpenSslBuildWithoutCert_GivesACallbackThatChoosesNoCertificate()
    {
        var handshakeOptions = await CaptureHandshakeOptionsAsync(matchesSchannelBuild: false);

        var selection = handshakeOptions.LocalCertificateSelectionCallback;
        Diagnostics.Act("selection callback", selection is null ? "none" : "given");
        Assert.IsNotNull(selection);
        Assert.IsNull(selection(this, "localhost", [], null, []));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_SchannelBuildWithoutCert_GivesNoCallback()
    {
        var handshakeOptions = await CaptureHandshakeOptionsAsync(matchesSchannelBuild: true);

        Diagnostics.Act("selection callback", handshakeOptions.LocalCertificateSelectionCallback is null ? "none" : "given");
        Assert.IsNull(handshakeOptions.LocalCertificateSelectionCallback);
    }

    private static async Task<SslClientAuthenticationOptions> CaptureHandshakeOptionsAsync(bool matchesSchannelBuild)
    {
        SslClientAuthenticationOptions? handshakeOptions = null;
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild)
        {
            AuthenticateSslStreamAsClientAsync = (_, authenticationOptions, _) =>
            {
                handshakeOptions = authenticationOptions;
                throw new AuthenticationException("The test stops before the handshake.");
            },
        };
        var (client, _) = InMemoryDuplexStream.CreatePair();

        await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, new IPEndPoint(IPAddress.Loopback, 443)), "localhost", CancellationToken.None);

        return handshakeOptions!;
    }
}
