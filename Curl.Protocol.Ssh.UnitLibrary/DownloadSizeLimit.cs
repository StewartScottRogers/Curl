using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh;

/// <summary>
/// The <c>--max-filesize</c> cut an SFTP or SCP download makes, as curl 8.21.0's download
/// writer (<c>cw_download_write</c> in <c>lib/sendf.c</c>) makes it: a write that would pass
/// the limit is cut at it, the bytes under it are written, and the transfer fails with
/// exit 63; a body exactly at the limit succeeds (BL-1327, BL-1328).
/// </summary>
/// <param name="maxFileSize">The <c>--max-filesize</c> limit; no limit when not given or 0.</param>
internal sealed class DownloadSizeLimit(long? maxFileSize)
{
    private readonly long limit = maxFileSize is > 0 and long given ? given : long.MaxValue;

    /// <summary>
    /// Gets how many of <paramref name="length" /> bytes may still be written after
    /// <paramref name="received" />; fewer than <paramref name="length" /> means the limit
    /// is passed.
    /// </summary>
    /// <param name="received">The bytes written so far.</param>
    /// <param name="length">The bytes about to be written.</param>
    /// <returns>The bytes that may be written.</returns>
    internal int AllowedOf(long received, int length) => (int)Math.Min(length, limit - received);

    /// <summary>
    /// Gets the failure curl reports once the limit is passed: exit 63, <c>Exceeded the
    /// maximum allowed file size (N) with N bytes</c>.
    /// </summary>
    /// <param name="received">The bytes written, the limit itself.</param>
    /// <returns>The failure, with <paramref name="received" /> as the bytes transferred.</returns>
    internal TransferResult Exceeded(long received) =>
        TransferResult.Failure(
            CurlExitCode.FilesizeExceeded,
            string.Create(CultureInfo.InvariantCulture, $"Exceeded the maximum allowed file size ({limit}) with {received} bytes"),
            received);
}
