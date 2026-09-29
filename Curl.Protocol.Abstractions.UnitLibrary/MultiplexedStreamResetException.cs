namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="IOException" /> <see cref="IMultiplexedStream.ReadAsync(Memory{byte}, CancellationToken)" />
/// throws when the peer resets the stream, carrying the peer's application error code
/// (ADR-0144).
/// </summary>
/// <remarks>
/// The HTTP handler turns it into curl's <c>HTTP/3 stream &lt;id&gt; reset by server</c>,
/// exit 95, or exit 18 once body bytes have arrived.
/// </remarks>
public sealed class MultiplexedStreamResetException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultiplexedStreamResetException" /> class.
    /// </summary>
    /// <param name="applicationErrorCode">The error code in the peer's <c>RESET_STREAM</c>.</param>
    /// <param name="message">The message that describes the reset.</param>
    public MultiplexedStreamResetException(long applicationErrorCode, string message)
        : base(message)
    {
        ApplicationErrorCode = applicationErrorCode;
    }

    /// <summary>
    /// Gets the application error code the peer's <c>RESET_STREAM</c> carried.
    /// </summary>
    public long ApplicationErrorCode { get; }
}
