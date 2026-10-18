using System.Buffers;
using System.Text;
using System.Text.Unicode;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Converts between a URL's bytes and .NET text without losing a byte: valid UTF-8 is its
/// characters, and each byte that is not part of valid UTF-8 is the lone surrogate
/// <c>U+DC80</c> to <c>U+DCFF</c> (<c>U+DC00</c> plus the byte), so <c>%c0</c> decoded and
/// encoded again is <c>%C0</c>, as libcurl 8.22.0's byte strings keep it (ADR-0695).
/// </summary>
public static class UrlByteText
{
    private const char EscapeBase = '\uDC00';

    private const char FirstEscapedByte = '\uDC80';

    private const char LastEscapedByte = '\uDCFF';

    /// <summary>The text of <paramref name="bytes" />: UTF-8, a byte that does not decode carried as <c>U+DC00</c> plus the byte.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text.</returns>
    public static string ToText(ReadOnlySpan<byte> bytes)
    {
        if (Utf8.IsValid(bytes))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        StringBuilder text = new(bytes.Length);
        Span<char> characters = stackalloc char[2];
        while (!bytes.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf8(bytes, out Rune rune, out int used);
            if (status == OperationStatus.Done)
            {
                text.Append(characters[..rune.EncodeToUtf16(characters)]);
            }
            else
            {
                AppendEscaped(text, bytes[..used]);
            }

            bytes = bytes[used..];
        }

        return text.ToString();
    }

    /// <summary>
    /// The bytes of <paramref name="text" />: a lone <c>U+DC80</c> to <c>U+DCFF</c> is the byte it
    /// carries, every other character its UTF-8 (a lone surrogate otherwise <c>U+FFFD</c>'s).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    public static byte[] ToBytes(string text)
    {
        List<byte> bytes = new(text.Length);
        Span<byte> utf8 = stackalloc byte[4];
        int index = 0;
        while (index < text.Length)
        {
            char character = text[index];
            if (character is >= FirstEscapedByte and <= LastEscapedByte)
            {
                bytes.Add((byte)(character - EscapeBase));
                index++;
                continue;
            }

            _ = Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out int used);
            bytes.AddRange(utf8[..rune.EncodeToUtf8(utf8)]);
            index += used;
        }

        return [.. bytes];
    }

    private static void AppendEscaped(StringBuilder text, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            text.Append((char)(EscapeBase + value));
        }
    }
}
