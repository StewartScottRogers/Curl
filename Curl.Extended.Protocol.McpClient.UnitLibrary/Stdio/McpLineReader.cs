namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>
/// Reads newline-delimited messages from a server's standard output ("Base Protocol &gt; Transports
/// &gt; stdio": messages are delimited by newlines). A carriage return before the newline is dropped,
/// and a last line the stream ends without a newline is still a line.
/// </summary>
public sealed class McpLineReader(Stream stream)
{
    private const int ChunkSize = 4096;

    private readonly Stream stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly byte[] chunk = new byte[ChunkSize];
    private int chunkStart;
    private int chunkEnd;

    /// <summary>Reads the next line without its line ending, or returns <see langword="null"/> at the end of the stream.</summary>
    public async Task<byte[]?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new MemoryStream();
        while (true)
        {
            if (chunkStart == chunkEnd)
            {
                chunkStart = 0;
                chunkEnd = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (chunkEnd == 0)
                {
                    return line.Length == 0 ? null : WithoutCarriageReturn(line);
                }
            }

            if (MoveBufferedBytesUpToNewline(line))
            {
                return WithoutCarriageReturn(line);
            }
        }
    }

    private bool MoveBufferedBytesUpToNewline(MemoryStream line)
    {
        var newline = Array.IndexOf(chunk, (byte)'\n', chunkStart, chunkEnd - chunkStart);
        if (newline < 0)
        {
            line.Write(chunk, chunkStart, chunkEnd - chunkStart);
            chunkStart = chunkEnd;
            return false;
        }

        line.Write(chunk, chunkStart, newline - chunkStart);
        chunkStart = newline + 1;
        return true;
    }

    private static byte[] WithoutCarriageReturn(MemoryStream line)
    {
        var bytes = line.ToArray();
        return bytes.Length > 0 && bytes[^1] == (byte)'\r' ? bytes[..^1] : bytes;
    }
}
