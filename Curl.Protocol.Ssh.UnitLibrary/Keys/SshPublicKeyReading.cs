namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// The outcome of reading the user's public key: the key, or libssh2's reason why the
/// <c>--pubkey</c> file gave none.
/// </summary>
/// <param name="Key">The public key, or <see langword="null" /> when none could be read.</param>
/// <param name="DenialReason">
/// libssh2 1.11.1's <c>file_read_publickey</c> message when the <c>--pubkey</c> file gave no
/// key; <see langword="null" /> when a key was read, or when the key was to be derived from
/// the private key, whose reason depends on the cryptography backend (ADR-0281).
/// </param>
internal readonly record struct SshPublicKeyReading(SshPublicKey? Key, string? DenialReason);
