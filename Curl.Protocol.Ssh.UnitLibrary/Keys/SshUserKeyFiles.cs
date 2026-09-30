namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// The key files curl hands libssh2 for <c>publickey</c> authentication.
/// </summary>
/// <param name="PrivateKeyPath">
/// <c>--key</c>, or the first of curl's default files that exists, or the empty string when
/// none does.
/// </param>
/// <param name="PublicKeyPath">
/// <c>--pubkey</c>, or <see langword="null" /> when it was not given or empty, so the
/// public key is derived from the private one.
/// </param>
internal sealed record SshUserKeyFiles(string PrivateKeyPath, string? PublicKeyPath);
