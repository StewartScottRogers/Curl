namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_SSH_HOSTKEYFUNCTION</c>: checks a server's SSH host key in place of the known-hosts
/// file. <c>CURLOPT_SSH_HOSTKEYDATA</c> is the delegate's closure.
/// </summary>
/// <param name="keyType">The key's type.</param>
/// <param name="key">The key's bytes, valid only during the call.</param>
/// <returns><see cref="SshKeyMatch.Ok"/> to accept the key or <see cref="SshKeyMatch.Mismatch"/> to reject it.</returns>
public delegate SshKeyMatch SshHostKeyCallback(SshKeyType keyType, ReadOnlySpan<byte> key);
