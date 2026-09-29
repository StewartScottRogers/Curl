namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// The key type of a known-hosts entry, or of the server's host key, as libssh2 1.11.1's
/// <c>knownhost.c</c> classifies it: the five types it recognizes by name, <c>RSA1</c> for
/// a key that starts with a digit, and <see cref="Unknown" /> for everything else,
/// <c>ssh-dss</c> and every certificate type included (measured 2026-09-29, BL-566).
/// </summary>
internal enum KnownHostKeyType
{
    /// <summary>
    /// A type libssh2 does not recognize; an entry of this type never matches a host key.
    /// </summary>
    Unknown,

    /// <summary>An SSH-1 RSA key, whose line starts with its bit count.</summary>
    Rsa1,

    /// <summary><c>ssh-rsa</c>.</summary>
    SshRsa,

    /// <summary><c>ecdsa-sha2-nistp256</c>.</summary>
    EcdsaNistP256,

    /// <summary><c>ecdsa-sha2-nistp384</c>.</summary>
    EcdsaNistP384,

    /// <summary><c>ecdsa-sha2-nistp521</c>.</summary>
    EcdsaNistP521,

    /// <summary><c>ssh-ed25519</c>.</summary>
    SshEd25519,
}
