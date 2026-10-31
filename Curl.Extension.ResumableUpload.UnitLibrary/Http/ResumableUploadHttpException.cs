namespace Curl.Extension.ResumableUpload.Http;

/// <summary>An HTTP/1.1 exchange failed for the reason <see cref="Failure"/> names.</summary>
public sealed class ResumableUploadHttpException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ResumableUploadHttpException"/> class.</summary>
    /// <param name="failure">Why the exchange failed.</param>
    /// <param name="message">A description of the failure.</param>
    public ResumableUploadHttpException(ResumableUploadHttpFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    /// <summary>Gets why the exchange failed.</summary>
    public ResumableUploadHttpFailure Failure { get; }
}
