namespace Curl.Extension.Masque;

/// <summary>
/// Thrown when a MASQUE tunnel cannot be set up or a proxy sends what RFC 9298 or RFC 9484
/// does not allow: a URI template Curl cannot expand, an upgrade the proxy refused, or a
/// malformed capsule.
/// </summary>
internal sealed class MasqueException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MasqueException" /> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="statusCode">The status the proxy answered the upgrade with, if it answered one.</param>
    public MasqueException(string message, int? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the status the proxy answered the upgrade with, or <see langword="null" /> when the failure is not an answer.
    /// </summary>
    public int? StatusCode { get; }
}
