namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>One walk of <c>curl_easy_nextheader</c> made after a round's transfer.</summary>
/// <param name="Origin">The <c>CURLH_*</c> origin bits.</param>
/// <param name="Request">The request number, <c>-1</c> for the last.</param>
public sealed record LibcurlEasyHeaderWalk(int Origin, int Request);
