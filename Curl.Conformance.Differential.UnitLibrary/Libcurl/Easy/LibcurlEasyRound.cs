namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>
/// One round of an easy-handle case: options set, one <c>curl_easy_perform</c>, then the info,
/// header queries and header walks read, and optionally <c>curl_easy_reset</c>.
/// </summary>
/// <param name="Options">The options, in the order they are set.</param>
/// <param name="Info">The <c>CURLINFO</c> numbers read after the transfer.</param>
/// <param name="HeaderQueries">The <c>curl_easy_header</c> calls.</param>
/// <param name="HeaderWalks">The <c>curl_easy_nextheader</c> walks.</param>
/// <param name="Resets">Whether the handle is reset after the round.</param>
public sealed record LibcurlEasyRound(
    IReadOnlyList<LibcurlEasyOption> Options,
    IReadOnlyList<int> Info,
    IReadOnlyList<LibcurlEasyHeaderQuery> HeaderQueries,
    IReadOnlyList<LibcurlEasyHeaderWalk> HeaderWalks,
    bool Resets);
