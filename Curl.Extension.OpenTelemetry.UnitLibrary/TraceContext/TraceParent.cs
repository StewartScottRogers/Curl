namespace Curl.Extension.OpenTelemetry.TraceContext;

/// <summary>
/// A <c>traceparent</c> field value (W3C Trace Context Level 1, §3.2): the trace-id, the parent-id the receiver takes
/// as its parent span, and the trace-flags, each as lowercase hexadecimal.
/// </summary>
/// <param name="TraceId">The 16-byte trace-id as 32 lowercase hex characters (§3.2.2.3).</param>
/// <param name="ParentId">The 8-byte parent-id as 16 lowercase hex characters (§3.2.2.4).</param>
/// <param name="Flags">The trace-flags as 2 lowercase hex characters (§3.2.2.5).</param>
internal sealed record TraceParent(string TraceId, string ParentId, string Flags)
{
    /// <summary>The field name (§3.2.1).</summary>
    internal const string FieldName = "traceparent";

    /// <summary>The trace-flags with only the sampled flag set (§3.2.2.5.1): Curl records every span it sends.</summary>
    internal const string Sampled = "01";

    private const int Version00Length = 55;

    /// <summary>Gives a fresh, sampled <c>traceparent</c>: a random trace-id and parent-id, neither all zeros (§3.2.2.3, §3.2.2.4).</summary>
    /// <param name="fillRandom">Fills a span with random bytes.</param>
    /// <returns>The new value.</returns>
    internal static TraceParent Fresh(Action<Span<byte>> fillRandom) =>
        new(RandomNonZeroHex(16, fillRandom), RandomNonZeroHex(8, fillRandom), Sampled);

    /// <summary>
    /// Reads a received <c>traceparent</c> value (§3.2.2, §3.2.4): surrounding whitespace is ignored, version <c>00</c>
    /// is exactly 55 characters, a later version may carry more after a <c>-</c>, and version <c>ff</c>, upper-case hex
    /// and an all-zero trace-id or parent-id are invalid.
    /// </summary>
    /// <param name="value">The field value.</param>
    /// <returns>The value read, or <see langword="null" /> when it is invalid.</returns>
    internal static TraceParent? Parse(string value)
    {
        var text = value.Trim(' ', '\t');
        if (!HasValidLayout(text))
        {
            return null;
        }

        var traceId = text.Substring(3, 32);
        var parentId = text.Substring(36, 16);
        return IsAllZeros(traceId) || IsAllZeros(parentId) ? null : new TraceParent(traceId, parentId, text.Substring(53, 2));
    }

    /// <summary>Writes the value as version <c>00</c> (§3.2.2): <c>00-</c>trace-id<c>-</c>parent-id<c>-</c>trace-flags.</summary>
    /// <returns>The field value.</returns>
    public override string ToString() => "00-" + TraceId + "-" + ParentId + "-" + Flags;

    private static bool HasValidLayout(string text) =>
        text.Length >= Version00Length && HasDashesBetweenFields(text) && HasHexFields(text) && HasValidVersionTail(text);

    private static bool HasDashesBetweenFields(string text) => text[2] == '-' && text[35] == '-' && text[52] == '-';

    private static bool HasHexFields(string text) =>
        IsLowerHex(text.AsSpan(0, 2)) && IsLowerHex(text.AsSpan(3, 32)) && IsLowerHex(text.AsSpan(36, 16)) && IsLowerHex(text.AsSpan(53, 2));

    private static bool HasValidVersionTail(string text) => text[..2] switch
    {
        "ff" => false,
        "00" => text.Length == Version00Length,
        _ => text.Length == Version00Length || text[Version00Length] == '-',
    };

    private static bool IsLowerHex(ReadOnlySpan<char> text) =>
        !text.ContainsAnyExcept("0123456789abcdef");

    private static bool IsAllZeros(string hex) => !hex.AsSpan().ContainsAnyExcept('0');

    private static string RandomNonZeroHex(int length, Action<Span<byte>> fillRandom)
    {
        var bytes = new byte[length];
        do
        {
            fillRandom(bytes);
        }
        while (!bytes.AsSpan().ContainsAnyExcept((byte)0));

        return Convert.ToHexStringLower(bytes);
    }
}
