namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>The fields of one <c>struct curl_header</c> a header query or walk returned.</summary>
/// <param name="Name">The name's bytes.</param>
/// <param name="Value">The value's bytes.</param>
/// <param name="Amount">How many headers of that name there are.</param>
/// <param name="Index">This header's index among them.</param>
/// <param name="Origin">The <c>CURLH_*</c> origin bit.</param>
public sealed record LibcurlEasyHeaderFields(byte[] Name, byte[] Value, long Amount, long Index, int Origin);
