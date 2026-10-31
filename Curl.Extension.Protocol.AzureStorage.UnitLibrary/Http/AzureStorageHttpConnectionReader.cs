using System.Buffers;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.AzureStorage.Http;

/// <summary>Reads lines and bytes from an <see cref="IConnection"/> through one buffer, so a line read never loses the bytes after it.</summary>
/// <param name="connection">The connection to read.</param>
internal sealed class AzureStorageHttpConnectionReader(IConnection connection)
{
    /// <summary>The longest line of a response head, in bytes, before the exchange fails with <see cref="AzureStorageHttpFailure.LineTooLong"/>.</summary>
    public const int MaxLineLength = 65536;

    private readonly byte[] buffer = new byte[16384];
    private int start;
    private int end;

    /// <summary>Reads one line ending in LF, without the LF or a CR before it, decoded as Latin-1.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The line, or <see langword="null"/> when the connection closed before the line ended.</returns>
    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new ArrayBufferWriter<byte>();
        while (start < end || await FillAsync(cancellationToken))
        {
            var available = buffer.AsMemory(start, end - start);
            var newline = available.Span.IndexOf((byte)'\n');
            var take = newline < 0 ? available.Length : newline;
            if (line.WrittenCount + take > MaxLineLength)
            {
                throw new AzureStorageHttpException(AzureStorageHttpFailure.LineTooLong, $"A response line is longer than {MaxLineLength} bytes.");
            }

            line.Write(available.Span[..take]);
            start += take;
            if (newline >= 0)
            {
                start++;
                return Encoding.Latin1.GetString(line.WrittenSpan.TrimEnd((byte)'\r'));
            }
        }

        return null;
    }

    /// <summary>Reads up to <paramref name="destination"/>'s length of bytes, buffered ones first.</summary>
    /// <param name="destination">Where the bytes go; must not be empty.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The number of bytes read, or zero once the connection has closed.</returns>
    public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (start == end && !await FillAsync(cancellationToken))
        {
            return 0;
        }

        var count = Math.Min(destination.Length, end - start);
        buffer.AsMemory(start, count).CopyTo(destination);
        start += count;
        return count;
    }

    private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
    {
        start = 0;
        end = await connection.ReadAsync(buffer, cancellationToken);
        return end > 0;
    }
}
