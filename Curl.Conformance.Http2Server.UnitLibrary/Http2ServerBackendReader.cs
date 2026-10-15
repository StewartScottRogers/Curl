using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// Reads the HTTP/1.1 reply the backend (the sws emulation) writes on its connection: lines up to
/// CRLF, a counted number of bytes, or everything up to the close.
/// </summary>
/// <param name="connection">The connection to the backend.</param>
internal sealed class Http2ServerBackendReader(IConnection connection)
{
    private readonly byte[] buffer = new byte[16384];

    private int start;

    private int end;

    /// <summary>
    /// Reads one line without its CR LF (or bare LF), or <see langword="null"/> when the backend
    /// closed before a line began. A line the close cuts short is returned as it is.
    /// </summary>
    internal async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        while (await FillAsync(cancellationToken).ConfigureAwait(false))
        {
            byte next = buffer[start++];
            if (next == '\n')
            {
                return Encoding.Latin1.GetString([.. line]).TrimEnd('\r');
            }

            line.Add(next);
        }

        return line.Count == 0 ? null : Encoding.Latin1.GetString([.. line]);
    }

    /// <summary>
    /// Reads <paramref name="count"/> bytes, or fewer when the backend closes first.
    /// </summary>
    internal async Task<byte[]> ReadBytesAsync(long count, CancellationToken cancellationToken)
    {
        var bytes = new MemoryStream();
        while (bytes.Length < count && await FillAsync(cancellationToken).ConfigureAwait(false))
        {
            int taken = (int)Math.Min(end - start, count - bytes.Length);
            bytes.Write(buffer, start, taken);
            start += taken;
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// Reads everything up to the backend's close.
    /// </summary>
    internal Task<byte[]> ReadToEndAsync(CancellationToken cancellationToken) => ReadBytesAsync(long.MaxValue, cancellationToken);

    // True when at least one unread byte is buffered, reading more from the backend if none is.
    private async Task<bool> FillAsync(CancellationToken cancellationToken)
    {
        if (start < end)
        {
            return true;
        }

        start = 0;
        end = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return end > 0;
    }
}
