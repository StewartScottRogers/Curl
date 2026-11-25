using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that writes response header bytes to the header output.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Writes head or trailer bytes to the header output one line at a time, each line with
    /// its line feed, as curl 8.21.0 calls its write callback once per header line; nothing
    /// when there is no header output. A failed write reports that line's length, so an
    /// <c>-i -o</c> file that cannot be opened gives <c>client returned ERROR on write of 17
    /// bytes</c> for <c>HTTP/1.1 200 OK</c> (BL-2092).
    /// </summary>
    /// <exception cref="HttpTransferException">The header output failed the write (exit 23).</exception>
    private static async ValueTask WriteHeadersAsync(Stream? headerOutput, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (headerOutput is null)
        {
            return;
        }

        while (!bytes.IsEmpty)
        {
            int lineFeed = bytes.Span.IndexOf((byte)'\n');
            // No line feed (-1 read as uint) is the whole remainder: the line ends at the last byte at the latest.
            int lineLength = (int)Math.Min((uint)lineFeed, (uint)(bytes.Length - 1)) + 1;
            await WriteHeaderLineAsync(headerOutput, bytes[..lineLength], cancellationToken).ConfigureAwait(false);
            bytes = bytes[lineLength..];
        }
    }

    /// <summary>Writes one header line to the header output in one write.</summary>
    /// <exception cref="HttpTransferException">The header output failed the write (exit 23).</exception>
    private static async ValueTask WriteHeaderLineAsync(Stream headerOutput, ReadOnlyMemory<byte> line, CancellationToken cancellationToken)
    {
        try
        {
            await headerOutput.WriteAsync(line, cancellationToken).ConfigureAwait(false);
        }
        catch (OutputWriteFailedException failure)
        {
            throw WriteFailed(line.Length, failure.BytesAccepted);
        }
        catch (IOException)
        {
            throw WriteFailed(line.Length, 0);
        }
    }

    private static HttpTransferException WriteFailed(int passed, int returned) =>
        new(CurlExitCode.WriteError, HttpTransferMessages.OutputWriteFailed(passed, returned));
}
