using Curl.Api.Codes;

namespace Curl.Engine;

/// <summary>
/// What setting one part of a URL handle gives: the <see cref="CurlUrlCode" /> and, when it is
/// <see cref="CurlUrlCode.Ok" />, the handle's new parts.
/// </summary>
/// <param name="Code">The call's result.</param>
/// <param name="Parts">The new parts, or <see langword="null" /> when <paramref name="Code" /> refuses the value.</param>
internal readonly record struct UrlSetResult(CurlUrlCode Code, UrlHandleParts? Parts)
{
    /// <summary>A value accepted.</summary>
    /// <param name="parts">The handle's new parts.</param>
    /// <returns>An <see cref="CurlUrlCode.Ok" /> result holding <paramref name="parts" />.</returns>
    public static UrlSetResult Set(UrlHandleParts parts) => new(CurlUrlCode.Ok, parts);

    /// <summary>A value refused; the handle keeps its parts.</summary>
    /// <param name="code">Why.</param>
    /// <returns>A result holding <paramref name="code" /> and no parts.</returns>
    public static UrlSetResult Refused(CurlUrlCode code) => new(code, null);
}
