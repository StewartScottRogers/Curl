namespace Curl.Tls;

/// <summary>What one TLS 1.2, 1.1 or 1.0 record carries once its protection is removed.</summary>
/// <param name="Type">The record header's content type.</param>
/// <param name="Content">The content.</param>
internal sealed record Tls12RecordContent(TlsContentType Type, byte[] Content);
