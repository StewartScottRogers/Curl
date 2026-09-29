namespace Curl.Http3;

/// <summary>
/// Thrown when a QPACK peer breaks RFC 9204: an encoded field section, an encoder stream
/// instruction or a decoder stream instruction cannot be interpreted.
/// <see cref="ErrorCode" /> is the connection error to close the connection with.
/// </summary>
public sealed class QpackException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QpackException" /> class.
    /// </summary>
    /// <param name="errorCode">The connection error the input calls for.</param>
    /// <param name="reason">Which rule the input broke.</param>
    public QpackException(QpackErrorCode errorCode, string reason)
        : base($"QPACK {errorCode}: {reason}.")
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the connection error the input calls for.
    /// </summary>
    public QpackErrorCode ErrorCode { get; }
}
