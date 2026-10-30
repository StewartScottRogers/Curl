namespace Curl.Extension.AzureStorage.Http;

/// <summary>An Azure Storage HTTP/1.1 exchange failed for the reason <see cref="Failure"/> names.</summary>
public sealed class AzureStorageHttpException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AzureStorageHttpException"/> class.</summary>
    /// <param name="failure">Why the exchange failed.</param>
    /// <param name="message">A description of the failure.</param>
    public AzureStorageHttpException(AzureStorageHttpFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    /// <summary>Gets why the exchange failed.</summary>
    public AzureStorageHttpFailure Failure { get; }
}
