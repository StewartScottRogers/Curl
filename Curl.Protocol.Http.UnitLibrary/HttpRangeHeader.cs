using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Formats the <c>Range</c> value curl 8.21.0 sends for <c>-C</c>/<c>--continue-at</c> and
/// <c>-r</c>/<c>--range</c> (measured, BL-178 Notes).
/// </summary>
internal static class HttpRangeHeader
{
    /// <summary>
    /// Formats the <c>Range</c> value for a transfer: <c>bytes=N-</c> for a resume from a
    /// positive offset, else <c>bytes=A-B</c>, <c>bytes=A-</c> or <c>bytes=-N</c> for a range.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <param name="sendsBody">
    /// <see langword="true" /> when the request has a body, for which curl sends a
    /// <c>Content-Range</c> instead; Curl sends neither (ADR-0041).
    /// </param>
    /// <returns>The value, or <see langword="null" /> to send no <c>Range</c>.</returns>
    internal static string? ValueFor(ITransferContext context, bool sendsBody)
    {
        if (sendsBody)
        {
            return null;
        }

        if (context.ResumeFrom is > 0 and long offset)
        {
            return string.Create(CultureInfo.InvariantCulture, $"bytes={offset}-");
        }

        return context.Range is { } range ? "bytes=" + Format(range) : null;
    }

    private static string Format(ByteRange range) =>
        range.Kind switch
        {
            ByteRangeKind.Bounded => string.Create(CultureInfo.InvariantCulture, $"{range.FirstBytePosition}-{range.LastBytePosition}"),
            ByteRangeKind.FromOffset => string.Create(CultureInfo.InvariantCulture, $"{range.FirstBytePosition}-"),
            _ => string.Create(CultureInfo.InvariantCulture, $"-{range.SuffixLength}"),
        };
}
