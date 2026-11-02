using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Conformance.Extended.Harness;

/// <summary>Makes the server certificates TLS routes present; curl accepts them under <c>-k</c>.</summary>
public static class ReferenceCertificate
{
    /// <summary>Creates a self-signed RSA 2048 certificate for <paramref name="hostName" />, valid for a day.</summary>
    /// <param name="hostName">The host name in its subject and subject alternative name.</param>
    /// <returns>The certificate with its private key, exported and loaded again so every platform's TLS server can use the key.</returns>
    public static X509Certificate2 CreateSelfSigned(string hostName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new($"CN={hostName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder alternativeNames = new();
        alternativeNames.AddDnsName(hostName);
        request.CertificateExtensions.Add(alternativeNames.Build());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 created = request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pkcs12), password: null);
    }
}
