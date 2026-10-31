namespace Curl.Extension.Protocol.AzureStorage.Bodies;

/// <summary>The <c>Error</c> body Azure Storage returns with a failed request.</summary>
/// <param name="Code">The error code, such as <c>BlobNotFound</c>.</param>
/// <param name="Message">The human-readable message, or empty when the body has none.</param>
public sealed record AzureStorageError(string Code, string Message);
