namespace Curl.Tls;

/// <summary>A ClientHello's OCSP <c>status_request</c> (RFC 6066 section 8).</summary>
/// <param name="ResponderIds">The DER-encoded OCSP responder IDs the client trusts; empty for any.</param>
/// <param name="RequestExtensions">The DER-encoded OCSP request extensions; empty for none.</param>
public sealed record OcspStatusRequest(IReadOnlyList<byte[]> ResponderIds, byte[] RequestExtensions);
