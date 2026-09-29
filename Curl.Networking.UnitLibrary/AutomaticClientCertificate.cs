using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Chooses the client certificate curl's <c>--ssl-auto-client-cert</c> presents: the first
/// certificate in the user's personal store (<c>CurrentUser\MY</c>, the keychain on macOS)
/// that has its private key, is valid now and may authenticate a client, as ADR-0191 decides.
/// </summary>
internal static class AutomaticClientCertificate
{
    /// <summary>The store the certificate is chosen from, as Schannel's automatic credential takes it.</summary>
    internal const string StoreName = "MY";

    private const string ClientAuthenticationUsage = "1.3.6.1.5.5.7.3.2";

    private const string AnyExtendedKeyUsage = "2.5.29.37.0";

    /// <summary>Opens the user's personal store and chooses the certificate to present.</summary>
    /// <param name="certificateStore">Opens the store.</param>
    /// <param name="now">The time the certificate must be valid at.</param>
    /// <returns>
    /// The chosen certificate, which the caller owns, or <see langword="null" /> when the store
    /// cannot be opened or holds none that qualifies; every other certificate is disposed.
    /// </returns>
    public static X509Certificate2? Choose(IClientCertificateStore certificateStore, DateTimeOffset now)
    {
        var certificates = certificateStore.OpenCertificates(ClientCertificateStoreLocation.CurrentUser, StoreName);
        if (certificates is null)
        {
            return null;
        }

        var chosen = certificates.FirstOrDefault(certificate => Qualifies(certificate, now));
        foreach (var certificate in certificates.Where(certificate => !ReferenceEquals(certificate, chosen)))
        {
            certificate.Dispose();
        }

        return chosen;
    }

    /// <summary>Tells whether a certificate may be presented automatically.</summary>
    /// <param name="certificate">A certificate from the store.</param>
    /// <param name="now">The time it must be valid at.</param>
    /// <returns>
    /// <see langword="true" /> when it has its private key, <paramref name="now" /> lies within its
    /// validity, and it has no extended key usage extension or one naming client authentication or
    /// any usage.
    /// </returns>
    internal static bool Qualifies(X509Certificate2 certificate, DateTimeOffset now) =>
        certificate.HasPrivateKey
        && certificate.NotBefore.ToUniversalTime() <= now.UtcDateTime
        && now.UtcDateTime <= certificate.NotAfter.ToUniversalTime()
        && AllowsClientAuthentication(certificate);

    private static bool AllowsClientAuthentication(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault() is not { } usages
        || usages.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>()
            .Any(usage => usage.Value is ClientAuthenticationUsage or AnyExtendedKeyUsage);
}
