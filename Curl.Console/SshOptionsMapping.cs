using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Maps the parsed command line's SSH options onto the <see cref="SshOptions" /> an <c>scp</c> or
/// <c>sftp</c> transfer carries (ADR-0122): <c>--key</c>, <c>--pubkey</c>, <c>--pass</c>,
/// <c>--hostpubmd5</c>, <c>--hostpubsha256</c> and <c>--compressed-ssh</c> verbatim, and the
/// known-hosts file the runner resolved.
/// </summary>
internal static class SshOptionsMapping
{
    /// <summary>Tells whether <paramref name="scheme" /> is served by the SSH handler.</summary>
    /// <param name="scheme">The transfer URL's scheme, lower case.</param>
    /// <returns><see langword="true" /> for <c>scp</c> and <c>sftp</c>.</returns>
    internal static bool IsSshScheme(string scheme) => scheme is "scp" or "sftp";

    /// <summary>Creates the SSH options of one transfer.</summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="knownHostsPath">
    /// The known-hosts file to check the host key against, or <see langword="null" /> when <c>-k</c>
    /// turned the check off or none was found and a host key fingerprint was given.
    /// </param>
    /// <returns>The options.</returns>
    internal static SshOptions FromCommandLine(CommandLineOptions options, string? knownHostsPath) =>
        new()
        {
            PrivateKeyPath = options.PrivateKey,
            PublicKeyPath = options.SshPublicKeyFile,
            PrivateKeyPassphrase = options.Passphrase,
            KnownHostsPath = knownHostsPath,
            HostPublicKeyMd5 = options.SshHostPublicKeyMd5,
            HostPublicKeySha256 = options.SshHostPublicKeySha256,
            Compression = options.SshCompression,
        };
}
