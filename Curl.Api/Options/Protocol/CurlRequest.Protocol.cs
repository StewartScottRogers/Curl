namespace Curl.Api;

public sealed partial record CurlRequest
{
    /// <summary>The HTTP settings for this request alone, replacing the client's <see cref="CurlClientOptions.Http"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlHttpOptions? Http { get; init; }

    /// <summary>The authentication settings for this request alone, replacing the client's <see cref="CurlClientOptions.Authentication"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlAuthenticationOptions? Authentication { get; init; }

    /// <summary>The FTP settings for this request alone, replacing the client's <see cref="CurlClientOptions.Ftp"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlFtpOptions? Ftp { get; init; }

    /// <summary>The SCP and SFTP settings for this request alone, replacing the client's <see cref="CurlClientOptions.Ssh"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlSshOptions? Ssh { get; init; }

    /// <summary>The SMTP settings for this request alone, replacing the client's <see cref="CurlClientOptions.Mail"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlMailOptions? Mail { get; init; }

    /// <summary>The TFTP settings for this request alone, replacing the client's <see cref="CurlClientOptions.Tftp"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlTftpOptions? Tftp { get; init; }

    /// <summary>The TELNET settings for this request alone, replacing the client's <see cref="CurlClientOptions.Telnet"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlTelnetOptions? Telnet { get; init; }

    /// <summary>The RTSP settings for this request alone, replacing the client's <see cref="CurlClientOptions.Rtsp"/>; <see langword="null"/> (the default) keeps the client's.</summary>
    public CurlRtspOptions? Rtsp { get; init; }

    /// <summary>The HTTP, authentication and protocol records above, for <see cref="CurlProtocolOptions.Apply"/>.</summary>
    internal CurlProtocolRecords ProtocolRecords => new(Http, Authentication, Ftp, Ssh, Mail, Tftp, Telnet, Rtsp);
}
