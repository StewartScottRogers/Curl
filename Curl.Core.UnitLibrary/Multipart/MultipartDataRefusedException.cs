namespace Curl.Core.Multipart;

/// <summary>
/// Thrown by a read of an <see cref="EncodedReadStream" /> whose encoder refuses the data it
/// reached, as <c>7bit</c> refuses a byte above 127. It is an <see cref="IOException" />, so
/// whoever sends the body meets it as the failed read it is.
/// </summary>
internal sealed class MultipartDataRefusedException : IOException
{
    /// <summary>Initialises the exception with curl's message for data it cannot send.</summary>
    internal MultipartDataRefusedException()
        : base(MultipartFormBodyBuilder.ReadFailedMessage)
    {
    }
}
