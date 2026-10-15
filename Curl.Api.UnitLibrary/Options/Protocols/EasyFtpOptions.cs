namespace Curl.Api.Options.Protocols;

/// <summary>
/// The FTP and FTPS options of one easy handle, each defaulting to libcurl 8.22.0's default.
/// Command lists are carried as libcurl takes them; sending them is the engine's job.
/// </summary>
public sealed record EasyFtpOptions
{
    /// <summary>
    /// <c>CURLOPT_FTPPORT</c>: the interface, address or host name (with an optional
    /// <c>:PORT[-PORT]</c> range) for active FTP's <c>PORT</c> or <c>EPRT</c>, where <c>-</c> means
    /// the control connection's address. Default <see langword="null"/>, meaning passive FTP.
    /// </summary>
    public string? ActiveAddress { get; init; }

    /// <summary>
    /// <c>CURLOPT_QUOTE</c>: commands sent after the transfer request is set up and before the
    /// transfer. Default empty (libcurl's <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> Quote { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_PREQUOTE</c>: commands sent just before the transfer command, after the type is
    /// set. Default empty (libcurl's <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> PreQuote { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_POSTQUOTE</c>: commands sent after the transfer. Default empty (libcurl's
    /// <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> PostQuote { get; init; } = [];

    /// <summary><c>CURLOPT_APPEND</c>: append to the remote file instead of overwriting it. Default <see langword="false"/>.</summary>
    public bool Append { get; init; }

    /// <summary><c>CURLOPT_FTP_USE_EPRT</c>: try <c>EPRT</c> (and <c>LPRT</c>) before <c>PORT</c> in active mode. Default <see langword="true"/>.</summary>
    public bool UseEprt { get; init; } = true;

    /// <summary><c>CURLOPT_FTP_USE_EPSV</c>: try <c>EPSV</c> before <c>PASV</c> in passive mode. Default <see langword="true"/>.</summary>
    public bool UseEpsv { get; init; } = true;

    /// <summary><c>CURLOPT_FTP_USE_PRET</c>: send <c>PRET</c> before <c>PASV</c> or <c>EPSV</c>. Default <see langword="false"/>.</summary>
    public bool UsePret { get; init; }

    /// <summary>
    /// <c>CURLOPT_FTP_CREATE_MISSING_DIRS</c>: whether to create missing remote directories.
    /// Default <see cref="FtpCreateMissingDirectories.None"/>.
    /// </summary>
    public FtpCreateMissingDirectories CreateMissingDirectories { get; init; } = FtpCreateMissingDirectories.None;

    /// <summary>
    /// <c>CURLOPT_FTP_ALTERNATIVE_TO_USER</c>: the command sent when <c>USER</c> and <c>PASS</c>
    /// fail. Default <see langword="null"/>, meaning none.
    /// </summary>
    public string? AlternativeToUser { get; init; }

    /// <summary>
    /// <c>CURLOPT_FTP_SKIP_PASV_IP</c>: ignore the address in the <c>PASV</c> reply and connect to
    /// the control connection's address. Default <see langword="true"/> (since curl 7.74.0).
    /// </summary>
    public bool SkipPasvIp { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_FTPSSLAUTH</c>: which of <c>AUTH SSL</c> and <c>AUTH TLS</c> to try first.
    /// Default <see cref="FtpSslAuthentication.Default"/>.
    /// </summary>
    public FtpSslAuthentication SslAuthentication { get; init; } = FtpSslAuthentication.Default;

    /// <summary>
    /// <c>CURLOPT_FTP_SSL_CCC</c>: whether to clear the command channel (<c>CCC</c>) after
    /// authenticating. Default <see cref="FtpClearCommandChannel.None"/>.
    /// </summary>
    public FtpClearCommandChannel ClearCommandChannel { get; init; } = FtpClearCommandChannel.None;

    /// <summary><c>CURLOPT_FTP_ACCOUNT</c>: the account sent with <c>ACCT</c> when the server asks. Default <see langword="null"/>.</summary>
    public string? Account { get; init; }

    /// <summary>
    /// <c>CURLOPT_FTP_FILEMETHOD</c>: how to reach a file in a remote directory. Default
    /// <see cref="FtpFileMethod.MultiCwd"/>.
    /// </summary>
    public FtpFileMethod FileMethod { get; init; } = FtpFileMethod.MultiCwd;
}
