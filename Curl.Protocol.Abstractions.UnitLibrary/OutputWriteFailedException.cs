namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="IOException" /> a transfer's output stream throws when a write to its
/// destination fails, carrying how many bytes of that write the destination accepted.
/// </summary>
/// <remarks>
/// A protocol handler reports a failed output write as curl does,
/// <c>curl: (23) Failure writing output to destination, passed N returned M</c>, where
/// <c>N</c> is the size of the write and <c>M</c> is <see cref="BytesAccepted" />. Because
/// the type derives from <see cref="IOException" />, a handler that catches
/// <see cref="IOException" /> still catches it.
/// </remarks>
public sealed class OutputWriteFailedException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutputWriteFailedException" /> class.
    /// </summary>
    /// <param name="bytesAccepted">
    /// How many bytes of the failed write the destination accepted; zero or more.
    /// </param>
    /// <param name="message">The message that describes the failure.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bytesAccepted" /> is negative.
    /// </exception>
    public OutputWriteFailedException(int bytesAccepted, string message)
        : base(message)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytesAccepted);

        BytesAccepted = bytesAccepted;
    }

    /// <summary>
    /// Gets how many bytes of the failed write the destination accepted before the write
    /// failed: curl's <c>returned M</c>. For standard output this is the room left in curl
    /// 8.21.0's 4096-byte stdio buffer when the write that overflowed it arrived, and 0 for
    /// a write that failed with nothing buffered before it. Never negative.
    /// </summary>
    public int BytesAccepted { get; }
}
