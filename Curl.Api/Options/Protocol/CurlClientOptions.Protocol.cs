namespace Curl.Api;

public sealed partial record CurlClientOptions
{
    /// <summary>The HTTP settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Http"/> replaces it.</summary>
    public CurlHttpOptions? Http { get; init; }

    /// <summary>The authentication settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Authentication"/> replaces it.</summary>
    public CurlAuthenticationOptions? Authentication { get; init; }

    /// <summary>The FTP settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Ftp"/> replaces it.</summary>
    public CurlFtpOptions? Ftp { get; init; }

    /// <summary>The SCP and SFTP settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Ssh"/> replaces it.</summary>
    public CurlSshOptions? Ssh { get; init; }

    /// <summary>The SMTP settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Mail"/> replaces it.</summary>
    public CurlMailOptions? Mail { get; init; }

    /// <summary>The TFTP settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Tftp"/> replaces it.</summary>
    public CurlTftpOptions? Tftp { get; init; }

    /// <summary>The TELNET settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Telnet"/> replaces it.</summary>
    public CurlTelnetOptions? Telnet { get; init; }

    /// <summary>The RTSP settings for every request; <see langword="null"/> (the default) keeps libcurl's. A request's <see cref="CurlRequest.Rtsp"/> replaces it.</summary>
    public CurlRtspOptions? Rtsp { get; init; }

    /// <summary>The HTTP, authentication and protocol records above, for <see cref="CurlProtocolOptions.Apply"/>.</summary>
    internal CurlProtocolRecords ProtocolRecords => new(Http, Authentication, Ftp, Ssh, Mail, Tftp, Telnet, Rtsp);
}
