using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITlsProvider" /> that can hand out a copy of itself trusting only the system
/// certificate store, for a <see cref="ConnectTarget" /> whose
/// <see cref="ConnectTarget.TrustsSystemStoreOnly" /> is set (CGR-289, ADR-0979).
/// </summary>
internal interface ISystemStoreTrustTlsProvider : ITlsProvider
{
    /// <summary>
    /// Returns a provider of the same build and settings, except that it ignores
    /// <c>--cacert</c>, <c>--capath</c> and a CA blob, so unless <c>-k</c> is given the server certificate must
    /// verify against the system store and the host name must match.
    /// </summary>
    /// <returns>The provider that trusts only the system store.</returns>
    ITlsProvider WithSystemStoreOnlyTrust();

    /// <summary>
    /// Returns <paramref name="options" /> with every trust setting beside the system store
    /// removed: no CA file, CA directory or CA blob. <c>-k</c> is kept, as curl 8.22.0's WinLDAP
    /// connection honours it (measured, BL-4044, ADR-0992).
    /// </summary>
    /// <param name="options">The settings a provider was built with.</param>
    /// <returns>The settings that trust only the system store.</returns>
    static TlsClientOptions SystemStoreOnly(TlsClientOptions options) =>
        options with
        {
            CaCertificateFile = null,
            CaCertificateDirectory = null,
            CaCertificateBlob = null,
        };
}
