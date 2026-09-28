using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Turns the text of <c>-r</c>/<c>--range</c> into the one <see cref="ByteRange" /> a
/// handler receives through <see cref="ITransferContext.Range" />, the way libcurl 8.21.0's
/// <c>Curl_range</c> reads it. This is the one place range text is parsed; a handler that
/// serves one range reads the result, and the HTTP handler sends the text itself
/// (<see cref="ITransferContext.RangeText" />), unparsed, as curl does.
/// </summary>
/// <remarks>
/// <para>
/// The text is an optional first byte position, a dash, and an optional last byte position,
/// each a run of ASCII digits that fits in a <see cref="long" />; anything after the last
/// position is ignored, so <c>2-3,5-6</c> is <c>2-3</c> and <c>1-2abc</c> is <c>1-2</c>. A
/// position that is not there, or does not fit, counts as absent. So <c>5-</c>,
/// <c>5-abc</c> and <c>3--1</c> start at 5 or 3 and run to the end; <c>-</c> and
/// <c>-99999999999999999999</c> are the whole resource; <c>-3-1</c> is the last 3 bytes.
/// </para>
/// <para>
/// It names no range, and the transfer fails with <see cref="NotDeliveredFailure" /> (exit 33,
/// <see cref="CurlExitCode.RangeError" />), when no dash follows the first position or where
/// it would be (<c>abc</c>, <c>a-3</c>, <c> 1-2</c>, <c>+1-2</c>, an overflowing first
/// position), when a suffix is zero (<c>-0</c>), when the last position precedes the first
/// (<c>3-1</c>), and for <c>0-9223372036854775807</c>, whose length does not fit. All of
/// these were measured against curl 8.21.0 on 2026-09-26 over <c>file://</c>, which is what
/// <c>Curl_range</c> serves. Over HTTP curl 8.21.0 parses nothing and sends every one of these
/// as typed with exit 0, so <c>Curl.Console</c> does not refuse an <c>http</c> or <c>https</c>
/// transfer's text (ADR-0044, BL-386 Notes).
/// </para>
/// </remarks>
public static class ByteRangeParser
{
    /// <summary>
    /// The message curl 8.21.0 reports with exit 33 for range text that names no range,
    /// as the text of <c>curl: (33) Requested range was not delivered by the server</c>.
    /// </summary>
    public const string NotDeliveredMessage = "Requested range was not delivered by the server";

    /// <summary>
    /// Gets the failure a transfer ends with when its range text names no range: exit 33
    /// (<see cref="CurlExitCode.RangeError" />) with <see cref="NotDeliveredMessage" />.
    /// </summary>
    public static TransferResult NotDeliveredFailure { get; } =
        TransferResult.Failure(CurlExitCode.RangeError, NotDeliveredMessage);

    /// <summary>Parses <paramref name="rangeText" /> into a range.</summary>
    /// <param name="rangeText">The <c>-r</c>/<c>--range</c> value as the command line recorded it.</param>
    /// <param name="range">The range; <see langword="null" /> when the text names none.</param>
    /// <returns>
    /// <see langword="true" /> when the text names a range; <see langword="false" /> when the
    /// transfer must fail with <see cref="NotDeliveredFailure" />. A <see cref="ByteRange" />
    /// factory never throws for text this returns <see langword="true" /> for.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="rangeText" /> is <see langword="null" />.</exception>
    public static bool TryParse(string rangeText, [NotNullWhen(true)] out ByteRange? range)
    {
        ArgumentNullException.ThrowIfNull(rangeText);

        range = null;
        ReadOnlySpan<char> rest = rangeText;
        bool hasFirst = TryReadPosition(ref rest, out long first);
        if (!rest.StartsWith('-'))
        {
            return false;
        }

        rest = rest[1..];
        if (!TryReadPosition(ref rest, out long last))
        {
            range = ByteRange.FromOffset(first);
            return true;
        }

        range = hasFirst ? BoundedOrNull(first, last) : SuffixOrNull(last);
        return range is not null;
    }

    private static ByteRange? BoundedOrNull(long first, long last) =>
        first > last || last - first == long.MaxValue ? null : ByteRange.Bounded(first, last);

    private static ByteRange? SuffixOrNull(long suffixLength) =>
        suffixLength == 0 ? null : ByteRange.Suffix(suffixLength);

    /// <summary>
    /// Reads the run of ASCII digits at the start of <paramref name="rest" /> and moves past
    /// it; leaves <paramref name="rest" /> alone when there is no run or it does not fit.
    /// </summary>
    private static bool TryReadPosition(ref ReadOnlySpan<char> rest, out long position)
    {
        int digitCount = rest.IndexOfAnyExceptInRange('0', '9');
        ReadOnlySpan<char> digits = digitCount < 0 ? rest : rest[..digitCount];
        if (digits.IsEmpty
            || !long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out position))
        {
            position = 0;
            return false;
        }

        rest = rest[digits.Length..];
        return true;
    }
}
