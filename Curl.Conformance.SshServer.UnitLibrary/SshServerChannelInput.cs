namespace Curl.Conformance.SshServer;

/// <summary>
/// The bytes a client sends on a session channel, read a byte, a line or a block at a time
/// however its <c>CHANNEL_DATA</c> messages split them, as <c>scp</c> reads its standard input.
/// </summary>
/// <param name="channel">The channel to read.</param>
internal sealed class SshServerChannelInput(SshServerSessionChannel channel)
{
    private byte[] pending = [];

    private int position;

    private bool ended;

    /// <summary>
    /// Reads the next byte.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The byte, or -1 once the client has sent <c>EOF</c> or <c>CLOSE</c>.</returns>
    internal async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        while (position == pending.Length)
        {
            if (ended)
            {
                return -1;
            }

            pending = await channel.ReadDataAsync(cancellationToken).ConfigureAwait(false);
            position = 0;
            ended = pending.Length == 0;
        }

        return pending[position++];
    }

    /// <summary>
    /// Reads up to and including the next line feed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The line without its line feed, or <see langword="null"/> when the input ends first.</returns>
    internal async ValueTask<byte[]?> ReadLineAsync(CancellationToken cancellationToken)
    {
        List<byte> line = [];
        int next;
        while ((next = await ReadByteAsync(cancellationToken).ConfigureAwait(false)) != '\n')
        {
            if (next < 0)
            {
                return null;
            }

            line.Add((byte)next);
        }

        return [.. line];
    }

    /// <summary>
    /// Reads <paramref name="count"/> bytes, or fewer when the input ends first.
    /// </summary>
    /// <param name="count">How many bytes to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes read.</returns>
    internal async ValueTask<byte[]> ReadBlockAsync(long count, CancellationToken cancellationToken)
    {
        MemoryStream block = new();
        int next;
        while (block.Length < count && (next = await ReadByteAsync(cancellationToken).ConfigureAwait(false)) >= 0)
        {
            block.WriteByte((byte)next);
        }

        return block.ToArray();
    }
}
