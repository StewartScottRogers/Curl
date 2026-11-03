namespace Curl.Conformance.Extended.DnsQuery.Encrypted;

/// <summary>An HTTP/1.1 request a <see cref="DohReferenceServer" /> received.</summary>
/// <param name="RequestLine">The request line, such as <c>POST /dns-query HTTP/1.1</c>.</param>
/// <param name="Headers">The header fields by name, matched without regard to case.</param>
/// <param name="Body">The body: the <c>Content-Length</c> octets after the head, none without that field.</param>
public sealed record DohReceivedRequest(string RequestLine, IReadOnlyDictionary<string, string> Headers, byte[] Body);
