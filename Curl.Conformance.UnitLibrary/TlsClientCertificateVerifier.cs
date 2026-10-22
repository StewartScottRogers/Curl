using System.Security.Cryptography.X509Certificates;

namespace Curl.Conformance;

/// <summary>
/// What a TLS stand-in does with the client certificate when, as stunnel at <c>verify = 2</c> in front of
/// upstream's <c>https-mtls</c> server, it demands one: every certificate a client presents is recorded in
/// <see cref="ReceivedCertificates"/>, and the handshake goes on only when the certificate chains to
/// <paramref name="authority"/>, the test CA (<c>%CERTDIR/certs/test-ca.crt</c>). No revocation check is made.
/// </summary>
/// <param name="authority">The certificate authority a client certificate must chain to.</param>
public sealed class TlsClientCertificateVerifier(X509Certificate2 authority)
{
    private readonly List<X509Certificate2> received = [];

    /// <summary>Gets every certificate a client presented, verified or not, in the order presented.</summary>
    public IReadOnlyList<X509Certificate2> ReceivedCertificates
    {
        get
        {
            lock (received)
            {
                return [.. received];
            }
        }
    }

    /// <summary>Records the client's certificate and says whether it chains to the authority.</summary>
    /// <param name="certificate">The certificate the client presented, or <see langword="null"/> when it presented none.</param>
    /// <returns><see langword="true"/> to go on with the handshake; <see langword="false"/> for no certificate or one the authority did not sign.</returns>
    public bool Verify(X509Certificate? certificate)
    {
        if (certificate is null)
        {
            return false;
        }

        X509Certificate2 presented = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        lock (received)
        {
            received.Add(presented);
        }

        using X509Chain chain = new();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(presented);
    }
}
