namespace Curl.Extension.GoogleCloudStorage.Http;

/// <summary>A Cloud Storage XML API HTTP/1.1 exchange failed for the reason <see cref="Failure"/> names.</summary>
public sealed class GcsHttpException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GcsHttpException"/> class.</summary>
    /// <param name="failure">Why the exchange failed.</param>
    /// <param name="message">A description of the failure.</param>
    public GcsHttpException(GcsHttpFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    /// <summary>Gets why the exchange failed.</summary>
    public GcsHttpFailure Failure { get; }
}
