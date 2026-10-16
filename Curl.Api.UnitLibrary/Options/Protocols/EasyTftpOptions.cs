namespace Curl.Api.Options.Protocols;

/// <summary>The TFTP options of one easy handle, each defaulting to libcurl 8.22.0's default.</summary>
public sealed record EasyTftpOptions
{
    /// <summary>
    /// <c>CURLOPT_TFTP_BLKSIZE</c>: the block size to ask the server for, 8 to 65464 bytes.
    /// Default 512.
    /// </summary>
    public int BlockSize { get; init; } = 512;

    /// <summary>
    /// <c>CURLOPT_TFTP_NO_OPTIONS</c>: send no TFTP options (RFC 2347) in the read request.
    /// Default <see langword="false"/>.
    /// </summary>
    public bool NoOptions { get; init; }
}
