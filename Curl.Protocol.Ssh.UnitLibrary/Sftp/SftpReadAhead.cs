namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// The reads libssh2 1.11.1 keeps in flight for curl 8.21.0 while it downloads a file:
/// four times curl's read size ahead, where the read size is 102400 bytes or, when the size
/// is known, what remains of it if less; in reads of at most 30000 bytes. Measured
/// 2026-09-29 (BL-569): a 5-byte file is read with one read of 20 bytes, a 10-byte one
/// with one of 40, and a 100000-byte or unknown-size one with 14 reads of 30000 in flight.
/// </summary>
/// <param name="session">The session.</param>
/// <param name="handle">The open file's handle.</param>
/// <param name="size">The file's size, or <see langword="null" /> when unknown.</param>
internal sealed class SftpReadAhead(SftpSession session, byte[] handle, long? size)
{
    /// <summary>The most bytes one read asks for, libssh2's <c>MAX_SFTP_READ_SIZE</c>.</summary>
    internal const int MaximumReadLength = 30000;

    /// <summary>The most bytes curl asks libssh2 for at a time, its download buffer.</summary>
    internal const int CurlReadSize = 102400;

    /// <summary>How many of curl's read sizes libssh2 asks for ahead.</summary>
    internal const int ReadAheadFactor = 4;

    private readonly Queue<(uint Id, long Offset, int Length)> inFlight = new();

    private long nextOffset;

    /// <summary>
    /// Tops the reads in flight up for what remains, then waits for the oldest one's
    /// answer. After an answer shorter than asked for, the reads still in flight are
    /// dropped and reading goes on from where it ended, as libssh2 does: measured, a
    /// 40-byte read that returned 5 bytes was followed by one at offset 5.
    /// </summary>
    /// <param name="received">How many bytes of the file have arrived so far.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The next bytes of the file; empty at its end.</returns>
    /// <exception cref="SshTransferException">Exit 79, <c>Error in the SSH layer</c>, for a failed read.</exception>
    /// <exception cref="InvalidDataException">The answer is malformed or of another type.</exception>
    internal async ValueTask<ReadOnlyMemory<byte>> ReadNextAsync(long received, CancellationToken cancellationToken)
    {
        long ahead = ReadAheadFactor * (size is { } known ? Math.Min(known - received, CurlReadSize) : CurlReadSize);
        int length = (int)Math.Min(ahead, MaximumReadLength);
        long count = (ahead + length - 1) / length;
        while (inFlight.Count < count)
        {
            uint id = await session.SendReadAsync(handle, nextOffset, length, cancellationToken).ConfigureAwait(false);
            inFlight.Enqueue((id, nextOffset, length));
            nextOffset += length;
        }

        (uint oldestId, long offset, int asked) = inFlight.Dequeue();
        ReadOnlyMemory<byte> data = await session.ReadDataAsync(oldestId, cancellationToken).ConfigureAwait(false);
        if (!data.IsEmpty && data.Length < asked)
        {
            inFlight.Clear();
            nextOffset = offset + data.Length;
        }

        return data;
    }
}
