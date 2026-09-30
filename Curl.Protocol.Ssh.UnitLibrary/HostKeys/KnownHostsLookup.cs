namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// The outcome of a known-hosts check and the key text of the entry it rests on, which
/// curl 8.21.0 prints in its <c>SSH: host check</c> line (ADR-0262).
/// </summary>
/// <param name="Check">Whether the host key matched, mismatched or was not found.</param>
/// <param name="Key">The entry's base64 key text; <see langword="null" /> when not found.</param>
internal sealed record KnownHostsLookup(KnownHostsCheck Check, string? Key);
