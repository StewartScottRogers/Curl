namespace Curl.Extension.ResumableUpload.Http;

/// <summary>One HTTP/1.1 request to a resumable upload server.</summary>
/// <param name="Method">The method, such as <c>POST</c>, <c>PATCH</c> or <c>HEAD</c>.</param>
/// <param name="Target">The request target in origin form, such as <c>/uploads/42</c>, already percent-encoded.</param>
/// <param name="Headers">The header fields to send, in order; they must include <c>Host</c>. <c>Content-Length</c> is added from <paramref name="BodyLength"/>.</param>
/// <param name="Body">The body to stream, or <see langword="null"/> for none; it must be seekable when <paramref name="BodyOffset"/> is not zero.</param>
/// <param name="BodyLength">The number of bytes of <paramref name="Body"/> to send, as <c>Content-Length</c>; ignored without a body.</param>
/// <param name="BodyOffset">The position in <paramref name="Body"/> the bytes are sent from, such as the server's <c>Upload-Offset</c> when resuming; zero sends from where the stream stands.</param>
public sealed record ResumableUploadHttpRequest(
    string Method,
    string Target,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    Stream? Body = null,
    long BodyLength = 0,
    long BodyOffset = 0);
