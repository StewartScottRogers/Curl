namespace Curl.Extension.AzureStorage.Requests;

/// <summary>Where an Azure Storage URL's request goes: always HTTPS to <see cref="Host"/>.</summary>
/// <param name="Host">The service endpoint's host, such as <c>account.blob.core.windows.net</c>.</param>
/// <param name="Path">The resource path, percent-encoded, such as <c>/container/blob</c>; <c>/</c> for the account itself.</param>
/// <param name="Query">The URL's query without its <c>?</c>, or empty for none.</param>
public sealed record AzureStorageEndpoint(string Host, string Path, string Query);
