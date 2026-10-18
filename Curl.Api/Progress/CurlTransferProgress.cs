namespace Curl.Api;

/// <summary>
/// One progress report of a transfer <see cref="CurlClient.SendAsync"/> performs, given to
/// <see cref="CurlRequest.Progress"/> each time the engine calls the transfer's
/// <c>CURLOPT_XFERINFOFUNCTION</c>. A total of 0 means the size is not known (yet), as in libcurl.
/// </summary>
public sealed record CurlTransferProgress
{
    /// <summary>The response body bytes expected, 0 when not known.</summary>
    public long DownloadTotal { get; init; }

    /// <summary>The response body bytes received so far.</summary>
    public long DownloadNow { get; init; }

    /// <summary>The request body bytes to send, 0 when not known.</summary>
    public long UploadTotal { get; init; }

    /// <summary>The request body bytes sent so far.</summary>
    public long UploadNow { get; init; }

    /// <summary>The average download speed so far in bytes per second (<c>CURLINFO_SPEED_DOWNLOAD_T</c>).</summary>
    public long DownloadSpeed { get; init; }

    /// <summary>The average upload speed so far in bytes per second (<c>CURLINFO_SPEED_UPLOAD_T</c>).</summary>
    public long UploadSpeed { get; init; }

    /// <summary>Which way the bytes are moving: <see cref="CurlTransferPhase.Downloading"/> once the response body has a size or a byte, else <see cref="CurlTransferPhase.Uploading"/> once a request body byte is sent, else <see cref="CurlTransferPhase.Connecting"/>.</summary>
    public CurlTransferPhase Phase { get; init; }
}
