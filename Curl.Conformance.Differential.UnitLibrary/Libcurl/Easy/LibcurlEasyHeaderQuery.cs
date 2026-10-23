namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>One <c>curl_easy_header</c> call made after a round's transfer.</summary>
/// <param name="Name">The header name; <see langword="null"/> passes <c>NULL</c>.</param>
/// <param name="Index">The index among headers of that name.</param>
/// <param name="Origin">The <c>CURLH_*</c> origin bits.</param>
/// <param name="Request">The request number, <c>-1</c> for the last.</param>
public sealed record LibcurlEasyHeaderQuery(string? Name, int Index, int Origin, int Request);
