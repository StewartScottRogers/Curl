namespace Curl.Api.Options.Behaviour;

/// <summary>
/// An easy handle's transfer options (ADR-0506, plan row <c>EasyTransferOptions</c>): what is
/// transferred (range, resume offset, custom method, body or not, upload and its size), buffer
/// sizes, the file size limit, time conditions, text and line-ending conversion, speed limits and
/// the whole-transfer timeout. Every default is libcurl 8.22.0's.
/// </summary>
public sealed record EasyTransferOptions
{
    /// <summary>
    /// <c>CURLOPT_RANGE</c>: the byte ranges to ask for, <c>X-Y</c> with several separated by
    /// commas. Default <see langword="null"/>: the whole resource.
    /// </summary>
    public string? Range { get; init; }

    /// <summary>
    /// <c>CURLOPT_RESUME_FROM</c> (and <c>CURLOPT_RESUME_FROM_LARGE</c>): the byte offset the
    /// transfer starts at. Default 0: the start; libcurl's -1 for an upload means the end of the
    /// remote file.
    /// </summary>
    public long ResumeFrom { get; init; }

    /// <summary>
    /// <c>CURLOPT_CUSTOMREQUEST</c>: the request method or command sent instead of the protocol's
    /// own. Default <see langword="null"/>: the protocol's own.
    /// </summary>
    public string? CustomRequest { get; init; }

    /// <summary>
    /// <c>CURLOPT_FILETIME</c>: whether libcurl asks for the remote file's modification time, read
    /// back with <c>CURLINFO_FILETIME</c>. Default <see langword="false"/> (0).
    /// </summary>
    public bool FileTime { get; init; }

    /// <summary>
    /// <c>CURLOPT_DIRLISTONLY</c>: whether a directory listing gives names only (FTP <c>NLST</c>,
    /// SFTP, POP3 <c>LIST</c>-only). Default <see langword="false"/> (0).
    /// </summary>
    public bool DirListOnly { get; init; }

    /// <summary>
    /// <c>CURLOPT_NOBODY</c>: whether the body is left out of the transfer (an HTTP <c>HEAD</c>).
    /// Default <see langword="false"/> (0).
    /// </summary>
    public bool NoBody { get; init; }

    /// <summary>
    /// <c>CURLOPT_UPLOAD</c>: whether the transfer is an upload (an HTTP <c>PUT</c>). Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool Upload { get; init; }

    /// <summary>
    /// <c>CURLOPT_INFILESIZE</c> (and <c>CURLOPT_INFILESIZE_LARGE</c>): the size in bytes of the
    /// file being uploaded. Default -1: unknown.
    /// </summary>
    public long InFileSize { get; init; } = -1;

    /// <summary>
    /// <c>CURLOPT_UPLOAD_BUFFERSIZE</c>: the upload buffer's size in bytes. Default 65536 (64 KB).
    /// </summary>
    public long UploadBufferSize { get; init; } = 65536;

    /// <summary>
    /// <c>CURLOPT_BUFFERSIZE</c>: the receive buffer's size in bytes, the most one write callback
    /// call is given. Default <c>CURL_MAX_WRITE_SIZE</c>, 16384 (16 KB).
    /// </summary>
    public long BufferSize { get; init; } = 16384;

    /// <summary>
    /// <c>CURLOPT_MAXFILESIZE</c> (and <c>CURLOPT_MAXFILESIZE_LARGE</c>): the largest file in bytes
    /// the transfer accepts before failing with <c>CURLE_FILESIZE_EXCEEDED</c>. Default 0: no
    /// limit.
    /// </summary>
    public long MaxFileSize { get; init; }

    /// <summary>
    /// <c>CURLOPT_TIMECONDITION</c>: how <see cref="TimeValue"/> conditions the transfer. Default
    /// <see cref="TimeCondition.None"/> (<c>CURL_TIMECOND_NONE</c>).
    /// </summary>
    public TimeCondition TimeCondition { get; init; } = TimeCondition.None;

    /// <summary>
    /// <c>CURLOPT_TIMEVALUE</c> (and <c>CURLOPT_TIMEVALUE_LARGE</c>): the time
    /// <see cref="TimeCondition"/> compares with, in seconds since 1970-01-01 UTC, as libcurl's
    /// 64-bit <c>time_t</c> holds it, so a time past 9999-12-31 is kept. Default 0: 1970-01-01.
    /// </summary>
    public long TimeValue { get; init; }

    /// <summary>
    /// <c>CURLOPT_TRANSFERTEXT</c>: whether FTP transfers in ASCII mode and LDAP gives text. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool TransferText { get; init; }

    /// <summary>
    /// <c>CURLOPT_CRLF</c>: whether line feeds in an upload are converted to CRLF. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool Crlf { get; init; }

    /// <summary>
    /// <c>CURLOPT_PROXY_TRANSFER_MODE</c>: whether an FTP transfer through an HTTP proxy appends
    /// <c>;type=a</c> or <c>;type=i</c> to the URL. Default <see langword="false"/> (0).
    /// </summary>
    public bool ProxyTransferMode { get; init; }

    /// <summary>
    /// <c>CURLOPT_LOW_SPEED_LIMIT</c>: the average speed in bytes per second below which, for
    /// <see cref="LowSpeedTime"/>, the transfer is aborted. Default 0: no limit.
    /// </summary>
    public long LowSpeedLimit { get; init; }

    /// <summary>
    /// <c>CURLOPT_LOW_SPEED_TIME</c>: how long the transfer may stay below
    /// <see cref="LowSpeedLimit"/> before <c>CURLE_OPERATION_TIMEDOUT</c>. Default zero: never.
    /// </summary>
    public TimeSpan LowSpeedTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// <c>CURLOPT_MAX_SEND_SPEED_LARGE</c>: the most bytes per second an upload sends on average.
    /// Default 0: unlimited.
    /// </summary>
    public long MaxSendSpeed { get; init; }

    /// <summary>
    /// <c>CURLOPT_MAX_RECV_SPEED_LARGE</c>: the most bytes per second a download receives on
    /// average. Default 0: unlimited.
    /// </summary>
    public long MaxReceiveSpeed { get; init; }

    /// <summary>
    /// <c>CURLOPT_TIMEOUT</c> (and <c>CURLOPT_TIMEOUT_MS</c>): the longest the whole transfer may
    /// take. Default zero: never times out.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.Zero;
}
