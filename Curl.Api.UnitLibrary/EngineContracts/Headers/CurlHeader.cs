namespace Curl.Api.EngineContracts.Headers;

/// <summary>One received header: <c>struct curl_header</c>.</summary>
/// <param name="Name"><c>name</c>: the header's name as the server cased it.</param>
/// <param name="Value"><c>value</c>: the header's value, with leading and trailing blanks removed.</param>
/// <param name="Amount"><c>amount</c>: how many headers of this name the response has.</param>
/// <param name="Index"><c>index</c>: which of them this is, from 0.</param>
/// <param name="Origin"><c>origin</c>: the one <c>CURLH_*</c> bit saying where it came from.</param>
public sealed record CurlHeader(string Name, string Value, int Amount, int Index, CurlHeaderOrigin Origin);
