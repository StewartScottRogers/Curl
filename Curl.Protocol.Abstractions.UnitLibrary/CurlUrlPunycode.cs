using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Encodes the code points of one host name label as punycode (RFC 3492's Bootstring with
/// its punycode parameters), without the <c>xn--</c> prefix: <c>ü</c> is <c>tda</c>; and
/// decodes such a label back to its code points.
/// </summary>
internal static class CurlUrlPunycode
{
    private const int Base = 36;
    private const int MinimumThreshold = 1;
    private const int MaximumThreshold = 26;
    private const int Skew = 38;
    private const int Damp = 700;
    private const int InitialBias = 72;
    private const int InitialCodePoint = 0x80;
    private const int MaximumCodePoint = 0x10FFFF;

    /// <summary>
    /// Encodes <paramref name="codePoints" />, which hold at least one that is not ASCII. The
    /// caller keeps a label to at most 63 code points, so no delta overflows.
    /// </summary>
    public static string Encode(IReadOnlyList<int> codePoints)
    {
        var output = new StringBuilder();
        foreach (int codePoint in codePoints.Where(codePoint => codePoint < InitialCodePoint))
        {
            output.Append((char)codePoint);
        }

        int basicCount = output.Length;
        if (basicCount > 0)
        {
            output.Append('-');
        }

        EncodeNonBasic(codePoints, basicCount, output);

        return output.ToString();
    }

    /// <summary>
    /// Decodes <paramref name="encoded" />, a lower-case ASCII label without its <c>xn--</c>
    /// prefix, to its code points (<c>tda</c> is <c>ü</c>), or returns <see langword="null" />
    /// when it is not punycode: a digit that is neither a letter nor a decimal digit, a number cut
    /// short (<c>zz</c>), or a delta past <c>U+10FFFF</c>.
    /// </summary>
    public static List<int>? Decode(string encoded)
    {
        int delimiter = encoded.LastIndexOf('-');
        var codePoints = new List<int>(encoded.Length);
        codePoints.AddRange(encoded[..Math.Max(delimiter, 0)].Select(basic => (int)basic));

        return DecodeNonBasic(encoded.AsSpan(delimiter + 1), codePoints) ? codePoints : null;
    }

    private static bool DecodeNonBasic(ReadOnlySpan<char> digits, List<int> codePoints)
    {
        long codePoint = InitialCodePoint;
        long index = 0;
        int bias = InitialBias;
        int position = 0;
        while (position < digits.Length)
        {
            long start = index;
            if (!TryReadDelta(digits, ref position, bias, ref index))
            {
                return false;
            }

            bias = Adapt((int)(index - start), codePoints.Count + 1, start == 0);
            codePoint += index / (codePoints.Count + 1);
            index %= codePoints.Count + 1;
            if (codePoint > MaximumCodePoint)
            {
                return false;
            }

            codePoints.Insert((int)index, (int)codePoint);
            index++;
        }

        return true;
    }

    /// <summary>
    /// Reads one variable-length delta at <paramref name="position" /> and adds it to
    /// <paramref name="index" />. A digit at least the threshold makes the weight grow only while
    /// the index is at least that weight, so neither passes <see cref="long" /> once the index is
    /// held to <see cref="int.MaxValue" />.
    /// </summary>
    private static bool TryReadDelta(ReadOnlySpan<char> digits, ref int position, int bias, ref long index)
    {
        long weight = 1;
        for (int k = Base; ; k += Base)
        {
            int digit = position < digits.Length ? DigitValue(digits[position++]) : -1;
            if (digit < 0)
            {
                return false;
            }

            index += digit * weight;
            int threshold = Math.Clamp(k - bias, MinimumThreshold, MaximumThreshold);
            if (index > int.MaxValue || digit < threshold)
            {
                return index <= int.MaxValue;
            }

            weight *= Base - threshold;
        }
    }

    private static int DigitValue(char digit) =>
        char.IsAsciiLetterLower(digit) ? digit - 'a'
        : char.IsAsciiDigit(digit) ? digit - '0' + 26
        : -1;

    private static void EncodeNonBasic(IReadOnlyList<int> codePoints, int basicCount, StringBuilder output)
    {
        int handled = basicCount;
        int next = InitialCodePoint;
        int delta = 0;
        int bias = InitialBias;
        while (handled < codePoints.Count)
        {
            int smallest = codePoints.Where(codePoint => codePoint >= next).Min();
            delta += (smallest - next) * (handled + 1);
            next = smallest;
            foreach (int codePoint in codePoints)
            {
                delta += codePoint < next ? 1 : 0;
                if (codePoint == next)
                {
                    AppendDelta(output, delta, bias);
                    bias = Adapt(delta, handled + 1, handled == basicCount);
                    delta = 0;
                    handled++;
                }
            }

            delta++;
            next++;
        }
    }

    private static void AppendDelta(StringBuilder output, int delta, int bias)
    {
        int remaining = delta;
        for (int position = Base; ; position += Base)
        {
            int threshold = Math.Clamp(position - bias, MinimumThreshold, MaximumThreshold);
            if (remaining < threshold)
            {
                break;
            }

            output.Append(Digit(threshold + ((remaining - threshold) % (Base - threshold))));
            remaining = (remaining - threshold) / (Base - threshold);
        }

        output.Append(Digit(remaining));
    }

    private static int Adapt(int delta, int handled, bool first)
    {
        int scaled = first ? delta / Damp : delta / 2;
        scaled += scaled / handled;
        int position = 0;
        while (scaled > ((Base - MinimumThreshold) * MaximumThreshold) / 2)
        {
            scaled /= Base - MinimumThreshold;
            position += Base;
        }

        return position + (((Base - MinimumThreshold + 1) * scaled) / (scaled + Skew));
    }

    private static char Digit(int value) => (char)(value < 26 ? 'a' + value : '0' + value - 26);
}
