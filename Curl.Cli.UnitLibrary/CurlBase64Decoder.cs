namespace Curl.Cli;

/// <summary>
/// Decodes base64 as strictly as curl 8.21.0's <c>curlx_base64_decode</c>, which the <c>64dec</c>
/// variable function uses: the text must be a whole number of four-character groups from the standard
/// alphabet, with at most two <c>=</c> of padding and only at its end; no white space and no URL-safe
/// alphabet are accepted, and unused low bits of the last group are ignored. Ported from
/// <c>lib/curlx/base64.c</c>.
/// </summary>
internal static class CurlBase64Decoder
{
    private const byte NotInAlphabet = 0xFF;

    private const byte Padding = (byte)'=';

    private static readonly byte[] SymbolValues = CreateSymbolValues();

    /// <summary>Decodes <paramref name="text"/>, or reports that curl would refuse it.</summary>
    /// <param name="text">The base64 text.</param>
    /// <param name="decoded">The decoded bytes when the text was accepted; otherwise empty.</param>
    /// <returns><see langword="true"/> when the text was decoded.</returns>
    internal static bool TryDecode(ReadOnlySpan<byte> text, out byte[] decoded)
    {
        decoded = [];
        if (text.Length == 0 || text.Length % 4 != 0)
        {
            return false;
        }

        int padding = CountPadding(text);
        if (padding > 2)
        {
            return false;
        }

        byte[] output = new byte[(text.Length / 4 * 3) - padding];
        if (!TryDecodeGroups(text, padding, output))
        {
            return false;
        }

        decoded = output;
        return true;
    }

    private static int CountPadding(ReadOnlySpan<byte> text)
    {
        int padding = 0;
        while (padding < 3 && text[text.Length - 1 - padding] == Padding)
        {
            padding++;
        }

        return padding;
    }

    /// <summary>
    /// Decodes each group of four into <paramref name="output"/>; the last group, when the text is padded,
    /// may hold <c>=</c>, but no more of them than <paramref name="padding"/> counted at the end.
    /// </summary>
    private static bool TryDecodeGroups(ReadOnlySpan<byte> text, int padding, byte[] output)
    {
        int lastGroup = (text.Length / 4) - 1;
        for (int group = 0; group <= lastGroup; group++)
        {
            int allowedPadding = group == lastGroup ? padding : 0;
            if (!TryDecodeGroup(text.Slice(group * 4, 4), allowedPadding, out int bits))
            {
                return false;
            }

            WriteGroup(bits, output, group * 3);
        }

        return true;
    }

    private static bool TryDecodeGroup(ReadOnlySpan<byte> group, int allowedPadding, out int bits)
    {
        bits = 0;
        int paddingLeft = allowedPadding;
        foreach (byte symbol in group)
        {
            if (!TryReadSymbol(symbol, ref paddingLeft, out byte value))
            {
                return false;
            }

            bits = (bits << 6) | value;
        }

        return true;
    }

    /// <summary>
    /// Reads one symbol's six bits; a <c>=</c> reads as zero while <paramref name="paddingLeft"/> allows one
    /// more, and is refused like any symbol outside the alphabet otherwise.
    /// </summary>
    private static bool TryReadSymbol(byte symbol, ref int paddingLeft, out byte value)
    {
        if (symbol == Padding && paddingLeft > 0)
        {
            paddingLeft--;
            value = 0;
            return true;
        }

        value = SymbolValues[symbol];
        return value != NotInAlphabet;
    }

    private static void WriteGroup(int bits, byte[] output, int offset)
    {
        for (int index = 0; index < 3 && offset + index < output.Length; index++)
        {
            output[offset + index] = (byte)(bits >> (16 - (8 * index)));
        }
    }

    private static byte[] CreateSymbolValues()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        byte[] values = new byte[256];
        Array.Fill(values, NotInAlphabet);
        for (int index = 0; index < alphabet.Length; index++)
        {
            values[alphabet[index]] = (byte)index;
        }

        return values;
    }
}
