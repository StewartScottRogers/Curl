using System.Text;

namespace Curl.Networking;

/// <summary>
/// Decodes one Punycode label (RFC 3492) as OpenSSL's <c>ossl_punycode_decode</c> does: unlike
/// <see cref="System.Globalization.IdnMapping" />, it takes an empty label and one of ASCII characters only.
/// </summary>
internal static class PunycodeLabel
{
    private const int Base = 36;

    private const int MinimumThreshold = 1;

    private const int MaximumThreshold = 26;

    // A longer label cannot fit the constraint OpenSSL decodes it into.
    private const int MostCodePoints = 255;

    /// <summary>Decodes a label.</summary>
    /// <param name="encoded">The label without its <c>xn--</c> prefix.</param>
    /// <returns>The decoded text, or <see langword="null" /> when it is not Punycode or decodes too long.</returns>
    internal static string? Decode(string encoded)
    {
        if (!Ascii.IsValid(encoded))
        {
            return null;
        }

        var delimiter = Math.Max(encoded.LastIndexOf('-'), 0);
        var codePoints = encoded[..delimiter].Select(character => (long)character).ToList();
        var position = delimiter == 0 ? 0 : delimiter + 1;
        return TryInsertCodePoints(encoded, position, codePoints) ? ToText(codePoints) : null;
    }

    // Reads the deltas after the basic characters, each placing one more code point; false when one does not read.
    private static bool TryInsertCodePoints(string encoded, int position, List<long> codePoints)
    {
        long next = 128;
        long index = 0;
        long bias = 72;
        while (position < encoded.Length && codePoints.Count <= MostCodePoints)
        {
            var previous = index;
            if (!TryReadDelta(encoded, ref position, bias, ref index))
            {
                return false;
            }

            var count = codePoints.Count + 1;
            bias = Adapt(index - previous, count, isFirst: previous == 0);
            next += index / count;
            index %= count;
            codePoints.Insert((int)index, next);
            index++;
        }

        return true;
    }

    private static string? ToText(List<long> codePoints) =>
        codePoints.Count <= MostCodePoints && codePoints.All(IsScalarValue)
            ? string.Concat(codePoints.Select(codePoint => new Rune((int)codePoint).ToString()))
            : null;

    private static bool IsScalarValue(long codePoint) => Rune.IsValid((int)Math.Min(codePoint, int.MaxValue));

    // Adds one variable-length integer to the index; false when it is cut short, holds no digit or overflows.
    private static bool TryReadDelta(string encoded, ref int position, long bias, ref long index)
    {
        long weight = 1;
        for (long k = Base; position < encoded.Length; k += Base)
        {
            var digit = DigitOf(encoded[position++]);
            index += digit * weight;
            if (digit < 0 || index > int.MaxValue)
            {
                return false;
            }

            var threshold = Math.Clamp(k - bias, MinimumThreshold, MaximumThreshold);
            if (digit < threshold)
            {
                return true;
            }

            // Capped, so the next digit's product cannot overflow; any digit but 0 then passes the index's limit.
            weight = Math.Min(weight * (Base - threshold), int.MaxValue + 1L);
        }

        return false;
    }

    private static int DigitOf(char character) =>
        char.IsAsciiLetter(character) ? (character | 0x20) - 'a'
        : char.IsAsciiDigit(character) ? character - '0' + 26
        : -1;

    private static long Adapt(long delta, int count, bool isFirst)
    {
        delta /= isFirst ? 700 : 2;
        delta += delta / count;
        long k = 0;
        while (delta > (Base - MinimumThreshold) * MaximumThreshold / 2)
        {
            delta /= Base - MinimumThreshold;
            k += Base;
        }

        return k + ((Base - MinimumThreshold + 1) * delta / (delta + 38));
    }
}
