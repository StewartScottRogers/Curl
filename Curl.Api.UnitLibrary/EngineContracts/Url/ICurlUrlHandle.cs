using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.Url;

/// <summary>
/// One URL handle of an <see cref="ICurlEngine"/> (<c>CURLU</c>, ADR-0506): a URL held as its
/// parts, parsed and rebuilt by libcurl's URL parser. Named apart from Curl's own <c>CurlUrl</c>
/// parser, which implements it. <see cref="IDisposable.Dispose"/> is <c>curl_url_cleanup</c>.
/// </summary>
public interface ICurlUrlHandle : IDisposable
{
    /// <summary><c>curl_url_get</c>: one part of the URL.</summary>
    /// <param name="part"><c>what</c>: the part to get.</param>
    /// <param name="flags"><c>flags</c>: how to return it.</param>
    /// <param name="value"><c>part</c>: the part's text, or <see langword="null"/> when the call fails.</param>
    /// <returns>The call's result; a <c>CURLUE_NO_*</c> code such as <see cref="CurlUrlCode.NoQuery"/> when the part is not set.</returns>
    CurlUrlCode Get(CurlUrlPart part, CurlUrlFlags flags, out string? value);

    /// <summary><c>curl_url_set</c>: set or clear one part of the URL; <see cref="CurlUrlPart.Url"/> parses a whole URL, relative ones against the current one.</summary>
    /// <param name="part"><c>what</c>: the part to set.</param>
    /// <param name="value"><c>part</c>: the part's new text, or <see langword="null"/> to clear it.</param>
    /// <param name="flags"><c>flags</c>: how to parse it.</param>
    /// <returns>The call's result; the handle is unchanged when it fails.</returns>
    CurlUrlCode Set(CurlUrlPart part, string? value, CurlUrlFlags flags);

    /// <summary><c>curl_url_dup</c>: a new handle holding the same parts.</summary>
    /// <returns>The copy; the caller disposes it.</returns>
    ICurlUrlHandle Duplicate();
}
