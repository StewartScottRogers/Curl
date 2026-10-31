namespace Curl.Extension.Protocol.AzureStorage.Requests;

/// <summary>An Azure Storage request ready to send: its request target and the header fields that address, version and authorize it.</summary>
/// <param name="Target">The request target in origin form, such as <c>/container/blob?sv=...&amp;sig=...</c>.</param>
/// <param name="Headers">The header fields, in order: <c>Host</c>, <c>x-ms-version</c>, <c>x-ms-date</c>, then <c>Authorization</c> for a bearer token.</param>
public sealed record AzureStorageAuthorizedRequest(string Target, IReadOnlyList<KeyValuePair<string, string>> Headers);
