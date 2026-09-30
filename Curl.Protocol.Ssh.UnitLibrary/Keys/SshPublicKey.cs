namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// A user's public key as <c>publickey</c> authentication sends it (RFC 4252 section 7):
/// its key type and its blob.
/// </summary>
/// <param name="KeyType">
/// The key type, such as <c>ssh-rsa</c> or <c>ecdsa-sha2-nistp256</c>: the first word of a
/// <c>--pubkey</c> file, or the type of the private key it was derived from.
/// </param>
/// <param name="Blob">The public key blob, sent as it is.</param>
internal sealed record SshPublicKey(string KeyType, byte[] Blob);
