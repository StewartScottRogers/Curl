using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Whether curl's OpenSSL build refuses a server certificate for its keyUsage: OpenSSL's TLS
/// server purpose check fails (<c>unsuitable certificate purpose (26)</c>) when the extension is
/// present and allows none of digitalSignature, keyEncipherment and keyAgreement (its KU_TLS mask). .NET on Linux does not
/// enforce it, and the Schannel build accepts such a certificate (BL-5009).
/// </summary>
internal static class OpenSslKeyUsageRefusal
{
    private const X509KeyUsageFlags ServerUse = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.KeyAgreement;

    /// <summary>Returns whether the certificate has a keyUsage extension that forbids TLS server use.</summary>
    /// <param name="serverCertificate">The certificate the server presented as its own.</param>
    /// <returns><see langword="true" /> when the extension is present and allows none of digitalSignature, keyEncipherment and keyAgreement.</returns>
    internal static bool ForbidsServerUse(X509Certificate2 serverCertificate) =>
        serverCertificate.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault() is { } usage
        && (usage.KeyUsages & ServerUse) == 0;
}
