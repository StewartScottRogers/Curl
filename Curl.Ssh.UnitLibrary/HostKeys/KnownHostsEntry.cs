using System.Security.Cryptography;
using System.Text;

namespace Curl.Ssh.HostKeys;

/// <summary>
/// One host name of one known-hosts line, as libssh2 1.11.1 stores it: a plain name
/// (<c>host</c> or <c>[host]:port</c>, one entry per comma-separated name), or a hashed
/// one (<c>|1|salt|hash</c>, the HMAC-SHA1 of the name keyed with the salt).
/// </summary>
/// <param name="PlainName">The name as written, or <see langword="null" /> for a hashed entry.</param>
/// <param name="Salt">The hashed entry's salt; empty for a plain entry.</param>
/// <param name="NameHash">The hashed entry's HMAC-SHA1 of the name; empty for a plain entry.</param>
/// <param name="KeyType">The key's type.</param>
/// <param name="Key">The key's base64 text as written, without its comment; for an SSH-1 key, the whole rest of the line.</param>
internal sealed record KnownHostsEntry(
    string? PlainName,
    byte[] Salt,
    byte[] NameHash,
    KnownHostKeyType KeyType,
    string Key)
{
    /// <summary>
    /// Gets the key type's name as written, which libssh2 keeps to write the line back; empty
    /// for an SSH-1 key, whose line has none.
    /// </summary>
    internal string TypeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the text after the key, which libssh2 keeps to write the line back;
    /// <see langword="null" /> when the line has none.
    /// </summary>
    internal string? Comment { get; init; }

    /// <summary>
    /// Tells whether this entry names <paramref name="name" />: a plain entry when it is the
    /// same text, case included; a hashed entry when the name's HMAC-SHA1 under the salt is
    /// the stored hash, which must be 20 bytes long to match anything.
    /// </summary>
    /// <param name="name">A host name, or <c>[host]:port</c>.</param>
    /// <returns><see langword="true" /> when this entry names it.</returns>
    internal bool Names(string name) =>
        PlainName is not null
            ? PlainName == name
            : NameHash.Length == HMACSHA1.HashSizeInBytes
                && CryptographicOperations.FixedTimeEquals(HMACSHA1.HashData(Salt, Encoding.UTF8.GetBytes(name)), NameHash);

    /// <summary>
    /// Writes the entry as <c>libssh2_knownhost_writefile</c>'s OpenSSH format does, one line
    /// per entry: the plain name or <c>|1|salt|hash</c> in base64, the type name (none for an
    /// SSH-1 key), the key, and the comment when there is one; no line ending.
    /// </summary>
    /// <returns>The line.</returns>
    internal string ToLine()
    {
        string name = PlainName ?? $"|1|{Convert.ToBase64String(Salt)}|{Convert.ToBase64String(NameHash)}";
        string key = KeyType == KnownHostKeyType.Rsa1 ? Key : $"{TypeName} {Key}";
        return Comment is { Length: > 0 } comment ? $"{name} {key} {comment}" : $"{name} {key}";
    }
}
