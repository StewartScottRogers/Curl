namespace Curl.Kerberos;

/// <summary>
/// The contents of an MIT <c>FILE:</c> credential cache: the principal <c>kinit</c> got
/// tickets for and the credentials it holds. <see cref="Dispose" /> zeroes every session key.
/// </summary>
/// <param name="kdcTimeOffset">The header's KDC time offset, or <see langword="null" /> when it has none.</param>
/// <param name="defaultPrincipal">The cache's default principal.</param>
/// <param name="credentials">The credentials, in file order.</param>
public sealed class CredentialCache(
    TimeSpan? kdcTimeOffset,
    KerberosPrincipal defaultPrincipal,
    IReadOnlyList<CachedCredential> credentials) : IDisposable
{
    /// <summary>
    /// Gets how far the KDC's clock is ahead of the client's, as the header's
    /// <c>DeltaTime</c> tag records it; <see langword="null" /> when the header has no such tag.
    /// </summary>
    public TimeSpan? KdcTimeOffset { get; } = kdcTimeOffset;

    /// <summary>Gets the principal the cache holds tickets for, e.g. <c>alice@EXAMPLE.TEST</c>.</summary>
    public KerberosPrincipal DefaultPrincipal { get; } = defaultPrincipal;

    /// <summary>Gets the credentials, in file order, configuration entries included.</summary>
    public IReadOnlyList<CachedCredential> Credentials { get; } = credentials;

    /// <summary>Zeroes every credential's session key.</summary>
    public void Dispose()
    {
        foreach (CachedCredential credential in Credentials)
        {
            credential.SessionKey.Dispose();
        }
    }
}
