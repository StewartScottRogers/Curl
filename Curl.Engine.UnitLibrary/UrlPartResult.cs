using Curl.Api.Codes;

namespace Curl.Engine;

/// <summary>
/// What reading one part of a URL handle gives: the <see cref="CurlUrlCode" /> and, when it is
/// <see cref="CurlUrlCode.Ok" />, the part's text.
/// </summary>
/// <param name="Code">The call's result.</param>
/// <param name="Value">The part's text, or <see langword="null" /> when <paramref name="Code" /> is not <see cref="CurlUrlCode.Ok" />.</param>
internal readonly record struct UrlPartResult(CurlUrlCode Code, string? Value)
{
    /// <summary>A part read successfully.</summary>
    /// <param name="value">The part's text.</param>
    /// <returns>An <see cref="CurlUrlCode.Ok" /> result holding <paramref name="value" />.</returns>
    public static UrlPartResult Found(string value) => new(CurlUrlCode.Ok, value);

    /// <summary>A part that could not be read.</summary>
    /// <param name="code">Why: a <c>CURLUE_NO_*</c> code for a part not set, or the error.</param>
    /// <returns>A result holding <paramref name="code" /> and no text.</returns>
    public static UrlPartResult Failed(CurlUrlCode code) => new(code, null);

    /// <summary>The part's text when it is set, else <paramref name="missing" />.</summary>
    /// <param name="value">The part's text, or <see langword="null" /> when it is not set.</param>
    /// <param name="missing">The <c>CURLUE_NO_*</c> code for the part.</param>
    /// <returns>The result.</returns>
    public static UrlPartResult FoundOr(string? value, CurlUrlCode missing) =>
        value is null ? Failed(missing) : Found(value);
}
