namespace Curl.Ssh.HostKeys;

/// <summary>
/// The outcome of a known-hosts check and the key text of the entry it rests on, which
/// curl 8.21.0 prints in its <c>SSH: host check</c> line (ADR-0262).
/// </summary>
/// <param name="Check">Whether the host key matched, mismatched or was not found.</param>
/// <param name="Key">The entry's base64 key text; <see langword="null" /> when not found.</param>
internal sealed record KnownHostsLookup(KnownHostsCheck Check, string? Key)
{
    /// <summary>
    /// Gets the entry the outcome rests on, the one libssh2 hands curl and the one
    /// <c>CURLKHSTAT_FINE_REPLACE</c> deletes; <see langword="null" /> when not found.
    /// </summary>
    internal KnownHostsEntry? Entry { get; init; }
}
