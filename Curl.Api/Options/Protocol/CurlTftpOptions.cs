using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// TFTP settings, mapped one to one onto <see cref="EasyTftpOptions"/> (BL-2503, ADR-0640). Each member
/// starts at libcurl 8.22.0's default. Set on <see cref="CurlClientOptions.Tftp"/> for every request or
/// on <see cref="CurlRequest.Tftp"/> for one; a request's record replaces the client's whole. An invalid
/// value throws as it is set.
/// </summary>
public sealed record CurlTftpOptions : IEasyOptionRecord<EasyTftpOptions>
{
    /// <summary><c>CURLOPT_TFTP_BLKSIZE</c>: the block size asked for, 8 to 65464 bytes. Default 512.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 8 to 65464.</exception>
    public int BlockSize
    {
        get;
        init => field = (int)TransportOptionGuard.InRange(value, 8, 65464, nameof(BlockSize));
    } = 512;

    /// <summary><c>CURLOPT_TFTP_NO_OPTIONS</c>: send no option extensions. Default <see langword="false"/>.</summary>
    public bool NoOptions { get; init; }

    /// <summary>An <see cref="EasyTftpOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's TFTP options.</returns>
    EasyTftpOptions IEasyOptionRecord<EasyTftpOptions>.ToEasy() => new() { BlockSize = BlockSize, NoOptions = NoOptions };
}
