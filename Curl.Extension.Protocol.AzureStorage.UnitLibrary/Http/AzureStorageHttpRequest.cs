namespace Curl.Extension.Protocol.AzureStorage.Http;

/// <summary>One HTTP/1.1 request to an Azure Storage endpoint.</summary>
/// <param name="Method">The method, such as <c>GET</c> or <c>PUT</c>.</param>
/// <param name="Target">The request target in origin form, such as <c>/container/blob?uploads</c>, already percent-encoded.</param>
/// <param name="Headers">The header fields to send, in order; they must include <c>Host</c>. <c>Content-Length</c> is added from <paramref name="BodyLength"/>.</param>
/// <param name="Body">The body to stream, or <see langword="null"/> for none.</param>
/// <param name="BodyLength">The number of bytes of <paramref name="Body"/> to send, as <c>Content-Length</c>; ignored without a body.</param>
public sealed record AzureStorageHttpRequest(
    string Method,
    string Target,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    Stream? Body = null,
    long BodyLength = 0);
