using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IKerberosKdcProxyTlsClient" />: an <see cref="SslStream" /> that
/// verifies the proxy's certificate and host name as MIT's <c>k5tls</c> module does, against the
/// realm's <c>http_anchors</c> roots alone when there are any, with no revocation check, and
/// against the system's trust store otherwise (ADR-0300). No ALPN is offered and no client
/// certificate is sent.
/// </summary>
internal sealed class KerberosKdcProxyTlsClient : IKerberosKdcProxyTlsClient
{
    /// <inheritdoc />
    public async Task<Stream> AuthenticateAsync(Stream plaintext, string host, X509Certificate2Collection? anchors, CancellationToken cancellationToken)
    {
        SslPolicyErrors refused = SslPolicyErrors.None;
        SslClientAuthenticationOptions options = new()
        {
            TargetHost = host,
            CertificateChainPolicy = ChainPolicyFor(anchors),
            RemoteCertificateValidationCallback = (_, _, _, errors) =>
            {
                refused = errors;
                return errors == SslPolicyErrors.None;
            },
        };
        SslStream secured = new(plaintext, leaveInnerStreamOpen: false);
        try
        {
            await secured.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
            return secured;
        }
        catch (Exception exception)
        {
            await secured.DisposeAsync().ConfigureAwait(false);
            throw exception is OperationCanceledException ? exception : HandshakeFailure(host, refused, exception);
        }
    }

    // SslStream reports a refused certificate as whatever sending its alert threw, so the
    // callback's verdict, when there is one, is what the message names.
    private static IOException HandshakeFailure(string host, SslPolicyErrors refused, Exception exception) =>
        new(
            refused == SslPolicyErrors.None
                ? $"The KDC proxy {host} failed the TLS handshake: {exception.Message}"
                : $"The KDC proxy {host} sent a certificate that does not verify: {refused}.",
            exception);

    private static X509ChainPolicy? ChainPolicyFor(X509Certificate2Collection? anchors)
    {
        if (anchors is null)
        {
            return null;
        }

        X509ChainPolicy policy = new()
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck,
        };
        policy.CustomTrustStore.AddRange(anchors);
        return policy;
    }
}
