namespace Curl.Tls;

/// <summary>What one protected TLS 1.3 record carries once its protection and padding are removed.</summary>
/// <param name="Type">The real content type, from inside the protection.</param>
/// <param name="Content">The content.</param>
public sealed record Tls13RecordContent(TlsContentType Type, byte[] Content);
