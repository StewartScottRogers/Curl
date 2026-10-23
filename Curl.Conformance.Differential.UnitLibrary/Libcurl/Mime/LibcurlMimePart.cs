namespace Curl.Conformance.Differential.Libcurl.Mime;

/// <summary>One <c>curl_mime_addpart</c> part of a MIME case and the calls made on it, in order.</summary>
/// <param name="Steps">The calls.</param>
public sealed record LibcurlMimePart(IReadOnlyList<LibcurlMimeStep> Steps);
