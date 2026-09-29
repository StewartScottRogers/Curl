namespace Curl.Authentication;

/// <summary>
/// Thrown when a SPNEGO token cannot be decoded. <see cref="Error" /> says why.
/// </summary>
internal sealed class SpnegoTokenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpnegoTokenException" /> class.
    /// </summary>
    /// <param name="error">Why the token could not be decoded.</param>
    public SpnegoTokenException(SpnegoTokenError error)
        : base($"SPNEGO token could not be decoded: {error}.")
    {
        Error = error;
    }

    /// <summary>Gets why the token could not be decoded.</summary>
    public SpnegoTokenError Error { get; }
}
