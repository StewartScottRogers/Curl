namespace Curl.Extension.Protocol.S3.Http;

/// <summary>An S3 HTTP/1.1 exchange failed for the reason <see cref="Failure"/> names.</summary>
public sealed class S3HttpException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="S3HttpException"/> class.</summary>
    /// <param name="failure">Why the exchange failed.</param>
    /// <param name="message">A description of the failure.</param>
    public S3HttpException(S3HttpFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    /// <summary>Gets why the exchange failed.</summary>
    public S3HttpFailure Failure { get; }
}
