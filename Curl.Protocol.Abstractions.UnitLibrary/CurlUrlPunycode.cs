using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Encodes the code points of one host name label as punycode (RFC 3492's Bootstring with
/// its punycode parameters), without the <c>xn--</c> prefix: <c>ü</c> is <c>tda</c>.
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
