using Curl.Api.Callbacks;
using Curl.Api.Options.Protocols;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Maps the option group's SSH options onto the <see cref="SshOptions" /> an <c>scp</c> or
/// <c>sftp</c> transfer carries (ADR-0122): <c>--key</c>, <c>--pubkey</c>, <c>--hostpubmd5</c>,
/// <c>--hostpubsha256</c> and <c>--compressed-ssh</c> from <see cref="EasySshOptions" />, <c>--pass</c>,
/// and the known-hosts file the runner resolved.
/// </summary>
internal static class SshOptionsMapping
{
    /// <summary>Tells whether <paramref name="scheme" /> is served by the SSH handler.</summary>
    /// <param name="scheme">The transfer URL's scheme, lower case.</param>
    /// <returns><see langword="true" /> for <c>scp</c> and <c>sftp</c>.</returns>
    internal static bool IsSshScheme(string scheme) => scheme is "scp" or "sftp";

    /// <summary>Creates the SSH options of one transfer.</summary>
    /// <param name="ssh">The option group's SSH options.</param>
    /// <param name="privateKeyPassphrase">The <c>--pass</c> passphrase, or <see langword="null" /> when not given.</param>
    /// <param name="knownHostsPath">
    /// The known-hosts file to check the host key against, or <see langword="null" /> when <c>-k</c>
    /// turned the check off or none was found and a host key fingerprint was given.
    /// </param>
    /// <returns>The options.</returns>
    internal static SshOptions Of(EasySshOptions ssh, string? privateKeyPassphrase, string? knownHostsPath) =>
        new()
        {
            PrivateKeyPath = ssh.PrivateKeyFile,
            PublicKeyPath = ssh.PublicKeyFile,
            PrivateKeyPassphrase = privateKeyPassphrase,
            KnownHostsPath = knownHostsPath,
            HostPublicKeyMd5 = ssh.HostPublicKeyMd5,
            HostPublicKeySha256 = ssh.HostPublicKeySha256,
            Compression = ssh.Compression,
        };

    /// <summary>
    /// Creates the SSH options of a library handle's <c>scp</c> or <c>sftp</c> transfer, as libcurl sets them
    /// up: the known-hosts file only when <c>CURLOPT_SSH_KNOWNHOSTS</c> names one (libcurl looks for no default
    /// file), and the handle's <c>CURLOPT_SSH_HOSTKEYFUNCTION</c> and <c>CURLOPT_SSH_KEYFUNCTION</c> through
    /// <see cref="CallbackSshHostKeys" /> (ADR-0623).
    /// </summary>
    /// <param name="ssh">The handle's SSH options.</param>
    /// <param name="callbacks">The handle's callbacks.</param>
    /// <param name="privateKeyPassphrase">The <c>CURLOPT_KEYPASSWD</c> passphrase, or <see langword="null" /> when not set.</param>
    /// <returns>The options.</returns>
    internal static SshOptions OfHandle(EasySshOptions ssh, EasyCallbackOptions callbacks, string? privateKeyPassphrase) =>
        Of(ssh, privateKeyPassphrase, ssh.KnownHostsFile) with
        {
            HostKeyAcceptance = CallbackSshHostKeys.AcceptanceOf(callbacks.SshHostKey),
            KnownHostDecision = CallbackSshHostKeys.DecisionOf(callbacks.SshKey),
        };
}
