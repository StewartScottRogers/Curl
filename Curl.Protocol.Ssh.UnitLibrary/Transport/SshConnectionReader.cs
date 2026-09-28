using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// Buffers what the peer sends so the identification exchange can read it line by line
/// and the packet layer byte by byte, without either reading past what it needs.
/// </summary>
/// <param name="connection">The connection to read from.</param>
internal sealed class SshConnectionReader(IConnection connection)
{
    private readonly byte[] buffer = new byte[4096];

    private int start;

    private int end;

    /// <summary>
    /// Reads one line ending in LF, and removes the LF and a CR before it.
    /// </summary>
    /// <param name="maximumLength">The longest line accepted, in bytes, the LF included.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The line, one character per byte (Latin-1, so every byte survives unchanged), or
    /// <see langword="null" /> when the peer closed before the LF or the line ran past
    /// <paramref name="maximumLength" />.
    /// </returns>
    internal async ValueTask<string?> ReadLineAsync(int maximumLength, CancellationToken cancellationToken)
    {
        List<byte> line = [];
        while (line.Count < maximumLength)
        {
            if (start == end && !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            byte next = buffer[start++];
            if (next == (byte)'\n')
            {
                return Encoding.Latin1.GetString([.. line]).TrimEnd('\r');
            }

            line.Add(next);
        }

        return null;
    }

    /// <summary>
    /// Reads exactly <paramref name="count" /> bytes.
    /// </summary>
    /// <param name="count">How many bytes to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="EndOfStreamException">The peer closed first.</exception>
    internal async ValueTask<byte[]> ReadExactlyAsync(int count, CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[count];
        int filled = 0;
        while (filled < count)
        {
            if (start == end && !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new EndOfStreamException("The SSH peer closed the connection inside a packet.");
            }

            int taken = Math.Min(count - filled, end - start);
            buffer.AsSpan(start, taken).CopyTo(bytes.AsSpan(filled));
            start += taken;
            filled += taken;
        }

        return bytes;
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        start = 0;
        end = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return end > 0;
    }
}
