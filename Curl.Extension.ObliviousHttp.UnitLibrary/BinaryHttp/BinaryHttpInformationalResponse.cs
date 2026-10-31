namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>An informational (1xx) response that precedes the final one (RFC 9292 section 3.5.1).</summary>
/// <param name="StatusCode">The status code, 100 to 199.</param>
/// <param name="Headers">Its header field lines, in order.</param>
public sealed record BinaryHttpInformationalResponse(int StatusCode, IReadOnlyList<BinaryHttpField> Headers);
