namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>An Azure Storage URL could not be mapped, or its request authorized, for the reason <see cref="Failure"/> names.</summary>
public sealed class AzureStorageRequestException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AzureStorageRequestException"/> class.</summary>
    /// <param name="failure">Why the mapping or authorization failed.</param>
    /// <param name="message">A description of the failure.</param>
    public AzureStorageRequestException(AzureStorageRequestFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    /// <summary>Gets why the mapping or authorization failed.</summary>
    public AzureStorageRequestFailure Failure { get; }
}
