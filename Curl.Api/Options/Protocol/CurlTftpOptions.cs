using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// TFTP settings, mapped one to one onto <see cref="EasyTftpOptions"/> (BL-2503, ADR-0640). Each member
/// starts at libcurl 8.22.0's default. Set on <see cref="CurlClientOptions.Tftp"/> for every request or
/// on <see cref="CurlRequest.Tftp"/> for one; a request's record replaces the client's whole. A negative block size
/// throws as it is set.
/// </summary>
public sealed record CurlTftpOptions : IEasyOptionRecord<EasyTftpOptions>
{
    /// <summary>
    /// <c>CURLOPT_TFTP_BLKSIZE</c>: the block size asked for, as libcurl 8.22.0 treats it. 8 to 65464 bytes is
    /// used as given; 0 to 7 falls back to the 512-byte default; above 65464 is clamped to 65464, as
    /// <c>curl --tftp-blksize</c> does. Default 512.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative (libcurl returns <c>CURLE_BAD_FUNCTION_ARGUMENT</c>).</exception>
    public int BlockSize
    {
        get;
        init
        {
            var size = (int)TransportOptionGuard.InRange(value, 0, int.MaxValue, nameof(BlockSize));
            field = size < 8 ? 512 : Math.Min(size, 65464);
        }
    } = 512;

    /// <summary><c>CURLOPT_TFTP_NO_OPTIONS</c>: send no option extensions. Default <see langword="false"/>.</summary>
    public bool NoOptions { get; init; }

    /// <summary>An <see cref="EasyTftpOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's TFTP options.</returns>
    EasyTftpOptions IEasyOptionRecord<EasyTftpOptions>.ToEasy() => new() { BlockSize = BlockSize, NoOptions = NoOptions };
}
