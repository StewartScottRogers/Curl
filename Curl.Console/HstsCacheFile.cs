using System.Text;
using Curl.Core.Hsts;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The <c>--hsts</c> file of a transfer, read into the run's <see cref="HstsTransferPolicy" /> before
/// the transfer and written from it after, as curl 8.21.0 loads the file when each transfer starts and
/// saves it when the transfer's handle closes.
/// </summary>
/// <remarks>
/// Measured on 2026-09-29 (BL-620 and BL-621 Notes, ADR-0218): a missing or unreadable file reads as
/// empty, the file is written after every transfer - one that failed to connect, or answered over plain
/// <c>http</c>, included - in the platform's line endings, a file that cannot be written is left alone
/// silently, and <c>--hsts ""</c> reads and writes nothing.
/// </remarks>
internal static class HstsCacheFile
{
    /// <summary>Reads <paramref name="file" /> into the cache, unless it is empty.</summary>
    /// <param name="file">The <c>--hsts</c> value.</param>
    /// <param name="hsts">The run's HSTS cache.</param>
    /// <param name="fileSystem">Opens the file.</param>
    /// <returns>A task that completes when the file is read.</returns>
    internal static async Task ReadAsync(string file, HstsTransferPolicy hsts, IFileSystem fileSystem)
    {
        if (file.Length == 0)
        {
            return;
        }

        FileOpenResult opened = await fileSystem.OpenForReadAsync(file, CancellationToken.None).ConfigureAwait(false);
        if (opened.Content is not { } content)
        {
            return;
        }

        await using (content.ConfigureAwait(false))
        {
            using StreamReader reader = new(content, Encoding.Latin1, detectEncodingFromByteOrderMarks: false);
            hsts.ReadFile(await reader.ReadToEndAsync().ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Replaces <paramref name="file" /> with the cache in curl's format, unless it is empty or curl
    /// would fail to write an expiry (<see cref="HstsTransferPolicy.FormatFile" />).
    /// </summary>
    /// <param name="file">The <c>--hsts</c> value.</param>
    /// <param name="hsts">The run's HSTS cache.</param>
    /// <param name="fileSystem">Opens the file.</param>
    /// <param name="runsOnWindows">Whether to write as curl's Windows build does, in CR LF and with its <c>gmtime</c> limit.</param>
    /// <returns>A task that completes when the file is written.</returns>
    internal static async Task WriteAsync(string file, HstsTransferPolicy hsts, IFileSystem fileSystem, bool runsOnWindows)
    {
        string? text = file.Length == 0
            ? null
            : runsOnWindows
                ? hsts.FormatFile("\r\n", HstsCache.LatestWritableExpiryOnWindows)
                : hsts.FormatFile("\n", HstsCache.LatestWritableExpiryOffWindows);
        if (text is null)
        {
            return;
        }

        FileOpenResult opened = await fileSystem
            .OpenForWriteAsync(file, FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, CancellationToken.None)
            .ConfigureAwait(false);
        if (opened.Content is not { } content)
        {
            return;
        }

        await using (content.ConfigureAwait(false))
        {
            await content.WriteAsync(Encoding.Latin1.GetBytes(text)).ConfigureAwait(false);
        }
    }
}
