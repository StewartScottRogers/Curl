using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// FTP and FTPS settings, mapped one to one onto <see cref="EasyFtpOptions"/> (BL-2503, ADR-0640). Each
/// member starts at libcurl 8.22.0's default. Set on <see cref="CurlClientOptions.Ftp"/> for every
/// request or on <see cref="CurlRequest.Ftp"/> for one; a request's record replaces the client's whole.
/// An invalid value throws as it is set.
/// </summary>
public sealed record CurlFtpOptions : IEasyOptionRecord<EasyFtpOptions>
{
    /// <summary><c>CURLOPT_FTPPORT</c>: the active-mode address, <c>-</c> for the control connection's. Default <see langword="null"/>: passive.</summary>
    public string? ActiveAddress { get; init; }

    /// <summary><c>CURLOPT_QUOTE</c>: commands sent before the transfer. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> Quote
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(Quote));
    } = [];

    /// <summary><c>CURLOPT_PREQUOTE</c>: commands sent after the transfer type is set. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> PreQuote
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(PreQuote));
    } = [];

    /// <summary><c>CURLOPT_POSTQUOTE</c>: commands sent after the transfer. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> PostQuote
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(PostQuote));
    } = [];

    /// <summary><c>CURLOPT_APPEND</c>: append to the remote file. Default <see langword="false"/>.</summary>
    public bool Append { get; init; }

    /// <summary><c>CURLOPT_FTP_USE_EPRT</c>: try <c>EPRT</c> before <c>PORT</c>. Default <see langword="true"/>.</summary>
    public bool UseEprt { get; init; } = true;

    /// <summary><c>CURLOPT_FTP_USE_EPSV</c>: try <c>EPSV</c> before <c>PASV</c>. Default <see langword="true"/>.</summary>
    public bool UseEpsv { get; init; } = true;

    /// <summary><c>CURLOPT_FTP_USE_PRET</c>: send <c>PRET</c> before <c>PASV</c>. Default <see langword="false"/>.</summary>
    public bool UsePret { get; init; }

    /// <summary><c>CURLOPT_FTP_CREATE_MISSING_DIRS</c>: create missing remote directories. Default <see cref="FtpCreateMissingDirectories.None"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="FtpCreateMissingDirectories"/> value.</exception>
    public FtpCreateMissingDirectories CreateMissingDirectories
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(CreateMissingDirectories));
    }

    /// <summary><c>CURLOPT_FTP_ALTERNATIVE_TO_USER</c>: the command sent when <c>USER</c> fails. Default <see langword="null"/>.</summary>
    public string? AlternativeToUser { get; init; }

    /// <summary><c>CURLOPT_FTP_SKIP_PASV_IP</c>: connect to the control host, not <c>PASV</c>'s address. Default <see langword="true"/>.</summary>
    public bool SkipPasvIp { get; init; } = true;

    /// <summary><c>CURLOPT_FTPSSLAUTH</c>: whether <c>AUTH SSL</c> or <c>AUTH TLS</c> is tried first. Default <see cref="FtpSslAuthentication.Default"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="FtpSslAuthentication"/> value.</exception>
    public FtpSslAuthentication SslAuthentication
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(SslAuthentication));
    }

    /// <summary><c>CURLOPT_FTP_SSL_CCC</c>: drop TLS on the control connection after authenticating. Default <see cref="FtpClearCommandChannel.None"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="FtpClearCommandChannel"/> value.</exception>
    public FtpClearCommandChannel ClearCommandChannel
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(ClearCommandChannel));
    }

    /// <summary><c>CURLOPT_FTP_ACCOUNT</c>: the <c>ACCT</c> data. Default <see langword="null"/>.</summary>
    public string? Account { get; init; }

    /// <summary><c>CURLOPT_FTP_FILEMETHOD</c>: how the remote path is reached. Default <see cref="FtpFileMethod.MultiCwd"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="FtpFileMethod"/> value.</exception>
    public FtpFileMethod FileMethod
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(FileMethod));
    } = FtpFileMethod.MultiCwd;

    /// <summary>An <see cref="EasyFtpOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's FTP options.</returns>
    EasyFtpOptions IEasyOptionRecord<EasyFtpOptions>.ToEasy() => new()
    {
        ActiveAddress = ActiveAddress,
        Quote = Quote,
        PreQuote = PreQuote,
        PostQuote = PostQuote,
        Append = Append,
        UseEprt = UseEprt,
        UseEpsv = UseEpsv,
        UsePret = UsePret,
        CreateMissingDirectories = CreateMissingDirectories,
        AlternativeToUser = AlternativeToUser,
        SkipPasvIp = SkipPasvIp,
        SslAuthentication = SslAuthentication,
        ClearCommandChannel = ClearCommandChannel,
        Account = Account,
        FileMethod = FileMethod,
    };
}
