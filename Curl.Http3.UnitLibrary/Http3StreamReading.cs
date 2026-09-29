namespace Curl.Http3;

/// <summary>
/// Reads QUIC variable-length integers straight off a stream, a byte at a time, so nothing
/// after the integer is taken from the stream.
/// </summary>
internal static class Http3StreamReading
{
    /// <summary>
    /// Reads one variable-length integer.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The value, or <see langword="null" /> when the stream ended before its first byte.</returns>
    /// <exception cref="EndOfStreamException">The stream ended inside the integer.</exception>
    public static async ValueTask<long?> ReadVariableLengthIntegerAsync(Stream stream, CancellationToken cancellationToken)
    {
        var encoding = new byte[8];
        if (await stream.ReadAsync(encoding.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) == 0)
        {
            return null;
        }

        var length = Http3VariableLengthInteger.GetLengthFromFirstByte(encoding[0]);
        await stream.ReadExactlyAsync(encoding.AsMemory(1, length - 1), cancellationToken).ConfigureAwait(false);
        return Http3VariableLengthInteger.Decode(encoding.AsSpan(0, length));
    }
}
