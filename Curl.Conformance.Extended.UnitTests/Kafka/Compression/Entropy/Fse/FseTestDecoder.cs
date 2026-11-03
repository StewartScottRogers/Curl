using System.Numerics;

namespace Curl.Conformance.Extended.UnitTests.Kafka.Compression.Entropy.Fse;

/// <summary>
/// A small zstd FSE decoder written from RFC 8878 section 4.1 for the encoder's tests: reads a table description,
/// spreads the counts into decoding states and decodes a backward bit stream on one or two interleaved states.
/// </summary>
internal static class FseTestDecoder
{
    /// <summary>Reads a table description into its counts (trailing zero counts omitted) and accuracy log.</summary>
    internal static (short[] Counts, int AccuracyLog) ReadDescription(byte[] description)
    {
        int position = 0;
        int accuracyLog = 5 + ReadForward(description, ref position, 4);
        int threshold = 1 << accuracyLog;
        int remaining = threshold + 1;
        int width = accuracyLog + 1;
        List<short> counts = [];
        bool previousWasZero = false;
        while (remaining > 1)
        {
            while (previousWasZero)
            {
                int run = ReadForward(description, ref position, 2);
                counts.AddRange(Enumerable.Repeat((short)0, run));
                previousWasZero = run == 3;
            }

            int maximum = (2 * threshold) - 1 - remaining;
            int peek = position;
            int value = ReadForward(description, ref peek, width);
            int low = value & (threshold - 1);
            int count = low < maximum ? low : (value >= threshold ? value - maximum : value);
            position += low < maximum ? width - 1 : width;
            counts.Add((short)(count - 1));
            remaining -= Math.Abs(count - 1);
            previousWasZero = count == 1;
            while (remaining < threshold)
            {
                width--;
                threshold >>= 1;
            }
        }

        Assert.AreEqual((position + 7) / 8, description.Length, "description length");
        return ([.. counts], accuracyLog);
    }

    /// <summary>Decodes <paramref name="symbolCount"/> symbols from <paramref name="stream"/>.</summary>
    internal static byte[] Decode(short[] counts, int accuracyLog, byte[] stream, int symbolCount, int stateCount)
    {
        (byte[] symbols, int[] baselines, int[] bits) = BuildStates(counts, accuracyLog);
        int position = ((stream.Length - 1) * 8) + BitOperations.Log2(stream[^1]);
        int[] states = new int[stateCount];
        for (int state = 0; state < stateCount; state++)
        {
            states[state] = ReadBackward(stream, ref position, accuracyLog);
        }

        byte[] output = new byte[symbolCount];
        for (int index = 0; index < symbolCount; index++)
        {
            int current = states[index % stateCount];
            output[index] = symbols[current];
            if (index < symbolCount - stateCount)
            {
                states[index % stateCount] = baselines[current] + ReadBackward(stream, ref position, bits[current]);
            }
        }

        Assert.AreEqual(0, position, "bits left unread");
        return output;
    }

    /// <summary>The symbol of each decoding state, as section 4.1.1 spreads them.</summary>
    internal static byte[] Symbols(short[] counts, int accuracyLog) => BuildStates(counts, accuracyLog).Symbols;

    private static (byte[] Symbols, int[] Baselines, int[] Bits) BuildStates(short[] counts, int accuracyLog)
    {
        int size = 1 << accuracyLog;
        byte[] symbols = new byte[size];
        int high = size - 1;
        for (int symbol = 0; symbol < counts.Length; symbol++)
        {
            if (counts[symbol] == -1)
            {
                symbols[high--] = (byte)symbol;
            }
        }

        int position = 0;
        for (int symbol = 0; symbol < counts.Length; symbol++)
        {
            for (int placed = 0; placed < counts[symbol]; placed++)
            {
                symbols[position] = (byte)symbol;
                do
                {
                    position = (position + (size >> 1) + (size >> 3) + 3) & (size - 1);
                }
                while (position > high);
            }
        }

        int[] next = [.. counts.Select(count => (int)Math.Abs(count))];
        int[] baselines = new int[size];
        int[] bits = new int[size];
        for (int state = 0; state < size; state++)
        {
            int number = next[symbols[state]]++;
            bits[state] = accuracyLog - BitOperations.Log2((uint)number);
            baselines[state] = (number << bits[state]) - size;
        }

        return (symbols, baselines, bits);
    }

    private static int ReadForward(byte[] source, ref int position, int width)
    {
        int value = 0;
        for (int bit = 0; bit < width; bit++, position++)
        {
            int index = position >> 3;
            value |= (index < source.Length ? (source[index] >> (position & 7)) & 1 : 0) << bit;
        }

        return value;
    }

    private static int ReadBackward(byte[] stream, ref int position, int width)
    {
        int value = 0;
        for (int bit = 0; bit < width; bit++)
        {
            position--;
            Assert.IsGreaterThanOrEqualTo(0, position, "read past the stream's start");
            value = (value << 1) | ((stream[position >> 3] >> (position & 7)) & 1);
        }

        return value;
    }
}
