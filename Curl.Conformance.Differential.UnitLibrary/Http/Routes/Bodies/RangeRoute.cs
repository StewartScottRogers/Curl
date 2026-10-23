using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>
/// server.py's <c>/range</c>: the 104-byte body <c>A</c> to <c>Z</c> four times. With a <c>Range</c> header
/// it reads <c>bytes=a-b</c> from the start of the value (a missing <c>a</c> is 0, a missing <c>b</c> the last
/// byte; <c>bytes=-5</c> is therefore 0 to 5, not a suffix), answers 206 with <c>Content-Range: bytes a-b/104</c>
/// and the bytes a to b clamped to the body as a Python slice is; a value that does not match, or a number
/// past 64 bits (which Python would still take), ends the
/// connection unanswered, as server.py's exception does. Without one it answers 200 with
/// <c>Accept-Ranges: bytes</c> and the whole body. The connection is otherwise kept.
/// </summary>
public sealed class RangeRoute : IDifferentialRoute
{
    private const string RangeUnit = "bytes=";

    private static readonly byte[] Body = [.. Enumerable.Repeat(0, 4).SelectMany(_ => Enumerable.Range('A', 26)).Select(letter => (byte)letter)];

    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/range";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        DifferentialHttpResponse response;
        if (!context.Request.Headers.TryGetValue("range", out var range) || range.Length == 0)
        {
            response = new(200, [("Accept-Ranges", "bytes")], Body);
        }
        else if (!TryParseRange(range, out var first, out var last))
        {
            return true;
        }
        else
        {
            response = new(206, [("Content-Range", string.Create(CultureInfo.InvariantCulture, $"bytes {first}-{last}/{Body.Length}")), ("Content-Type", "text/plain")], Slice(first, last));
        }

        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    private static bool TryParseRange(string range, out long first, out long last)
    {
        last = Body.Length - 1;
        if (!range.StartsWith(RangeUnit, StringComparison.Ordinal))
        {
            first = 0;
            return false;
        }

        var firstDigits = LeadingDigits(range.AsSpan(RangeUnit.Length));
        var rest = range.AsSpan(RangeUnit.Length + firstDigits.Length);
        var lastDigits = rest.StartsWith('-') ? LeadingDigits(rest[1..]) : [];
        return TryParseOptional(firstDigits, 0, out first)
            & rest.StartsWith('-')
            & TryParseOptional(lastDigits, last, out last);
    }

    private static ReadOnlySpan<char> LeadingDigits(ReadOnlySpan<char> text)
    {
        var length = text.IndexOfAnyExceptInRange('0', '9');
        return length < 0 ? text : text[..length];
    }

    private static bool TryParseOptional(ReadOnlySpan<char> digits, long whenEmpty, out long value)
    {
        value = whenEmpty;
        return digits.IsEmpty || long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static byte[] Slice(long first, long last)
    {
        var start = (int)Math.Min(first, Body.Length);
        var end = (int)Math.Clamp(Math.Min(last, Body.Length) + 1, start, Body.Length);
        return Body[start..end];
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RangeRoute());
}
