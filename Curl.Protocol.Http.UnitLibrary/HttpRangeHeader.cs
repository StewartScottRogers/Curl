using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Formats the <c>Range</c> value curl 8.21.0 sends for <c>-C</c>/<c>--continue-at</c> and
/// <c>-r</c>/<c>--range</c>, and the <c>Content-Range</c> value it sends for <c>-r</c> on a request
/// with a body (measured, BL-178 and BL-306 Notes).
/// </summary>
internal static class HttpRangeHeader
{
    /// <summary>
    /// Formats the <c>Range</c> value for a transfer: <c>bytes=N-</c> for a resume from a
    /// positive offset, else <c>bytes=A-B</c>, <c>bytes=A-</c> or <c>bytes=-N</c> for a range.
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <param name="sendsBody">
    /// <see langword="true" /> when the request has a body, for which curl sends no <c>Range</c>
    /// (a <c>Content-Range</c> instead, <see cref="ContentRangeFor" />; ADR-0044).
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

    /// <summary>
    /// Formats the <c>Content-Range</c> value curl sends for <c>-r</c> on a request with a body:
    /// <c>bytes R/L</c>, where R is the range as given and L the body's length, or -1 when it is
    /// unknown, such as <c>bytes 0-9/1</c> for <c>-d x -r 0-9</c>.
    /// </summary>
    /// <param name="range">The <c>-r</c> range.</param>
    /// <param name="bodyLength">The body's length, or <see langword="null" /> when it is unknown.</param>
    /// <returns>The value.</returns>
    internal static string ContentRangeFor(ByteRange range, long? bodyLength) =>
        string.Create(CultureInfo.InvariantCulture, $"bytes {Format(range)}/{bodyLength ?? -1}");

    private static string Format(ByteRange range) =>
        range.Kind switch
        {
            ByteRangeKind.Bounded => string.Create(CultureInfo.InvariantCulture, $"{range.FirstBytePosition}-{range.LastBytePosition}"),
            ByteRangeKind.FromOffset => string.Create(CultureInfo.InvariantCulture, $"{range.FirstBytePosition}-"),
            _ => string.Create(CultureInfo.InvariantCulture, $"-{range.SuffixLength}"),
        };
}
