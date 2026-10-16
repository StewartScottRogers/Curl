namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_SSH_KEYFUNCTION</c>: decides on a server's SSH host key after libcurl has checked it
/// against <c>CURLOPT_SSH_KNOWNHOSTS</c>. <c>CURLOPT_SSH_KEYDATA</c> is the delegate's closure.
/// </summary>
/// <param name="knownKey">The key the known-hosts file holds for the host, or <see langword="null"/> when it has none.</param>
/// <param name="foundKey">The key the server presented.</param>
/// <param name="match">How <paramref name="foundKey"/> compared with the file.</param>
/// <returns>Whether to accept, reject or defer the key, and whether to update the file.</returns>
public delegate SshKeyStatus SshKeyCallback(SshKey? knownKey, SshKey foundKey, SshKeyMatch match);
