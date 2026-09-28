namespace Curl.Protocol.Abstractions;

/// <summary>
/// The SSH-only options of one transfer, filled by the command-line layer and read only by
/// the <c>scp</c> and <c>sftp</c> handler (ADR-0122).
/// </summary>
/// <remarks>
/// <see cref="ITransferContext.Ssh" /> being <see langword="null" /> means the scheme is not
/// <c>scp</c> or <c>sftp</c>, and the SSH handler treats it exactly as
/// <c>new SshOptions()</c>, every member at curl's "not given" value. <c>-u</c>/<c>--user</c>
/// stays on <see cref="ITransferContext.Credentials" />, <c>-Q</c> on
/// <see cref="ITransferContext.QuoteCommands" />, <c>-T</c> on
/// <see cref="ITransferContext.Upload" />, <c>-r</c> on <see cref="ITransferContext.Range" />,
/// <c>-C</c> on <see cref="ITransferContext.ResumeFrom" />, <c>--create-file-mode</c> on
/// <see cref="ITransferContext.CreateFileMode" />, <c>--ftp-create-dirs</c> on
/// <see cref="ITransferContext.FtpCreateDirectories" /> and <c>-l</c> on
/// <see cref="ITransferContext.ListOnly" />; none is repeated here.
/// </remarks>
public sealed record SshOptions
{
    /// <summary>
    /// Gets the private key file from <c>--key</c>, verbatim, or <see langword="null" />
    /// when it was not given.
    /// </summary>
    public string? PrivateKeyPath { get; init; }

    /// <summary>
    /// Gets the public key file from <c>--pubkey</c>, verbatim, or <see langword="null" />
    /// when it was not given and the public key is derived from the private one.
    /// </summary>
    public string? PublicKeyPath { get; init; }

    /// <summary>
    /// Gets the private key passphrase from <c>--pass</c>, or <see langword="null" /> when
    /// it was not given.
    /// </summary>
    public string? PrivateKeyPassphrase { get; init; }

    /// <summary>
    /// Gets the known-hosts file the server's host key is checked against, or
    /// <see langword="null" /> when <c>-k</c>/<c>--insecure</c> turns the check off.
    /// </summary>
    /// <remarks>
    /// The console, not the handler, resolves curl's default <c>~/.ssh/known_hosts</c> and
    /// fails with exit 2 when that file is missing and <c>-k</c> was not given (ADR-0122).
    /// </remarks>
    public string? KnownHostsPath { get; init; }

    /// <summary>
    /// Gets the expected MD5 fingerprint of the server's host key from <c>--hostpubmd5</c>,
    /// 32 hexadecimal digits, or <see langword="null" /> when it was not given.
    /// </summary>
    public string? HostPublicKeyMd5 { get; init; }

    /// <summary>
    /// Gets the expected SHA-256 fingerprint of the server's host key from
    /// <c>--hostpubsha256</c>, base64, or <see langword="null" /> when it was not given.
    /// </summary>
    public string? HostPublicKeySha256 { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--compressed-ssh</c> was given, so the handler
    /// offers zlib compression before <c>none</c>.
    /// </summary>
    public bool Compression { get; init; }
}
