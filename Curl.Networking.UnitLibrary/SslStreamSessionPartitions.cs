namespace Curl.Networking;

/// <summary>
/// Decides whether an <c>SslStream</c> handshake may resume a TLS session, so a run resumes a
/// session only between connections to one host whose verification settings match, as curl
/// keys its session cache on them (CGR-164, ADR-0860).
/// </summary>
/// <remarks>
/// <c>SslStream</c> keeps sessions in a process-wide cache keyed on the target host and the
/// credential, not on how the peer is verified, so a <c>-k</c> transfer's session would be
/// resumed by a later <c>--cacert</c> or <c>--pinnedpubkey</c> one. The first verification
/// settings to reach a host own its resumable sessions; a handshake to that host with other
/// settings turns resumption off (<c>SslClientAuthenticationOptions.AllowTlsResume</c>), which
/// gives it a credential of its own and a full handshake. Such a connection's session is never
/// resumed, where curl would resume it for a third transfer with the same settings.
/// </remarks>
internal sealed class SslStreamSessionPartitions
{
    private readonly Dictionary<string, string> _ownerByHost = new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _gate = new();

    /// <summary>Gets the partitions every provider of this process shares, as <c>SslStream</c>'s cache is shared.</summary>
    internal static SslStreamSessionPartitions Process { get; } = new();

    /// <summary>
    /// Returns whether a handshake to <paramref name="targetHost" /> with <paramref name="options" />
    /// may resume a session: when no handshake to that host came before it, or the first one had
    /// the same <see cref="VerificationKey" />.
    /// </summary>
    /// <param name="targetHost">The host the handshake is made to.</param>
    /// <param name="options">The handshake's TLS options.</param>
    /// <returns><see langword="true" /> to allow resumption.</returns>
    internal bool MayResume(string targetHost, TlsClientOptions options)
    {
        var key = VerificationKey(options);
        lock (_gate)
        {
            return _ownerByHost.TryAdd(targetHost, key) || _ownerByHost[targetHost] == key;
        }
    }

    /// <summary>
    /// Returns the settings curl's session cache tells peers apart by that decide how the peer is
    /// verified or who the client is: <c>-k</c>, <c>--cert-status</c>, the CA file, blob and
    /// directory, <c>--pinnedpubkey</c> and the client certificate and key.
    /// </summary>
    /// <param name="options">The handshake's TLS options.</param>
    /// <returns>The key, equal for two option sets exactly when those settings are.</returns>
    internal static string VerificationKey(TlsClientOptions options) => string.Join(
        '\n',
        options.Insecure,
        options.ChecksHostNameWhenInsecure,
        options.RequireCertificateStatus,
        FullPath(options.CaCertificateFile),
        options.CaCertificateBlob is { } blob ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(blob.Span)) : string.Empty,
        FullPath(options.CaCertificateDirectory),
        options.PinnedPublicKey,
        FullPath(options.ClientCertificate),
        FullPath(options.PrivateKey));

    private static string FullPath(string? path) =>
        string.IsNullOrEmpty(path) ? string.Empty : Path.GetFullPath(path);
}
