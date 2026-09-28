using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Formats the <c>Range</c> value curl 8.21.0 sends for <c>-C</c>/<c>--continue-at</c> and
/// <c>-r</c>/<c>--range</c>, and the <c>Content-Range</c> value it sends for <c>-r</c> on a request
/// with a body (measured, BL-178, BL-306 and BL-386 Notes). The <c>-r</c> text goes out as typed,
/// unparsed, as libcurl copies <c>data->state.range</c> into both headers.
/// </summary>
internal static class HttpRangeHeader
{
    /// <summary>
    /// Formats the <c>Range</c> value for a transfer: <c>bytes=N-</c> for a resume from a
    /// positive offset, else <c>bytes=</c> and the <c>-r</c> text as typed, such as
    /// <c>bytes=0-9,20-29</c>.
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

        return context.RangeText is { } rangeText ? "bytes=" + rangeText : null;
    }

    /// <summary>
    /// Formats the <c>Content-Range</c> value curl sends for <c>-r</c> on a request with a body:
    /// <c>bytes R/L</c>, where R is the <c>-r</c> text as typed and L the body's length, or -1
    /// when it is unknown, such as <c>bytes 0-9,20-29/1</c> for <c>-d x -r 0-9,20-29</c>.
    /// </summary>
    /// <param name="rangeText">The <c>-r</c> text.</param>
    /// <param name="bodyLength">The body's length, or <see langword="null" /> when it is unknown.</param>
    /// <returns>The value.</returns>
    internal static string ContentRangeFor(string rangeText, long? bodyLength) =>
        string.Create(CultureInfo.InvariantCulture, $"bytes {rangeText}/{bodyLength ?? -1}");
}
