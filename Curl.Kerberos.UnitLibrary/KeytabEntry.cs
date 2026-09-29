namespace Curl.Kerberos;

/// <summary>One key in a keytab: whose it is, which version, and the key itself.</summary>
/// <param name="Principal">The principal the key belongs to.</param>
/// <param name="Timestamp">When the key was written to the keytab.</param>
/// <param name="KeyVersionNumber">
/// The key version number: the entry's trailing 32-bit field when present and not zero,
/// otherwise its 8-bit field, as MIT reads it.
/// </param>
/// <param name="Key">The key and its encryption type.</param>
public sealed record KeytabEntry(
    KerberosPrincipal Principal,
    DateTimeOffset Timestamp,
    uint KeyVersionNumber,
    KerberosKey Key);
