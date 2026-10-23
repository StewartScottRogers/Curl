namespace Curl.Conformance.Differential.Libcurl.Scenarios;

/// <summary>
/// The libcurl calls a <see cref="LibcurlScenario"/> makes, so the same scenarios replay against
/// Curl's Libcurl and against the reference libcurl 8.22.0 (ADR-0791). Codes are the C
/// <c>CURLcode</c>, <c>CURLMcode</c>, <c>CURLSHcode</c> and <c>CURLUcode</c> numbers.
/// </summary>
public interface ILibcurlScenarioTarget
{
    /// <summary>Gets <c>curl_version()</c>.</summary>
    string Version();

    /// <summary>Gets <c>curl_easy_strerror(code)</c>.</summary>
    string EasyErrorText(int code);

    /// <summary>Gets <c>curl_multi_strerror(code)</c>.</summary>
    string MultiErrorText(int code);

    /// <summary>Gets <c>curl_share_strerror(code)</c>.</summary>
    string ShareErrorText(int code);

    /// <summary>Gets <c>curl_url_strerror(code)</c>.</summary>
    string UrlErrorText(int code);

    /// <summary>Creates a URL handle with <c>curl_url()</c>.</summary>
    ILibcurlScenarioUrl CreateUrl();

    /// <summary>Gets <c>curl_easy_escape(NULL, bytes, length)</c>.</summary>
    string Escape(byte[] bytes);

    /// <summary>Gets <c>curl_easy_unescape(NULL, bytes, length, &amp;outlength)</c>, or <see langword="null"/> when it returns NULL.</summary>
    byte[]? Unescape(byte[] bytes);

    /// <summary>Gets <c>curl_getdate(text, NULL)</c>.</summary>
    long ParseDate(string text);
}
