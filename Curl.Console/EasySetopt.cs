using Libcurl.Options;

namespace Curl.Console;

/// <summary>One <c>curl_easy_setopt</c> call the curl tool makes on a transfer's easy handle.</summary>
/// <param name="Option">The <c>CURLOPT_*</c> to set.</param>
/// <param name="Value">The value to set it to, as <see cref="Libcurl.Easy.CurlEasy.SetOption" /> takes it.</param>
internal readonly record struct EasySetopt(CurlOption Option, object? Value);
