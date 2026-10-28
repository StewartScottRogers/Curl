using System.Buffers;
using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// UTF-8 that keeps every byte (the "surrogate escape" scheme): <see cref="Decode" /> reads bytes as
/// UTF-8 but turns each byte <c>b</c> outside a valid sequence into the lone surrogate U+DC00 + <c>b</c>,
/// and <see cref="Encode" /> turns each lone surrogate U+DC80 to U+DCFF back into its byte; any other
/// lone surrogate encodes as U+FFFD, as <see cref="Encoding.UTF8" /> does, so valid text encodes exactly
/// as UTF-8. It is the one implementation behind file paths, arguments, header files and credentials.
/// It decodes whole buffers only: <see cref="Encoding" /> has no chunk-aware decoder, so a
/// <see cref="StreamReader" /> or a <see cref="Decoder" /> fed a multi-byte sequence split across two
/// buffers escapes both halves instead of joining them.
/// </summary>
public static class SurrogateEscapeUtf8
{
    /// <summary>The code page of UTF-8.</summary>
    private const int Utf8CodePage = 65001;

    /// <summary>The first escaped byte's surrogate: U+DC00 + 0x80, since a byte below 0x80 is always valid.</summary>
    private const char FirstEscape = '\uDC80';

    /// <summary>The last escaped byte's surrogate, U+DC00 + 0xFF.</summary>
    private const char LastEscape = '\uDCFF';

    /// <summary>The offset from a byte to its surrogate.</summary>
    private const int SurrogateOffset = 0xDC00;

    /// <summary>
    /// An <see cref="Encoding" /> that encodes and decodes as <see cref="Encode" /> and <see cref="Decode" />
    /// do, and reports UTF-8's code page. It handles whole buffers only.
    /// </summary>
    public static Encoding Encoding { get; } = new EscapingEncoding();

    /// <summary>Reads <paramref name="bytes" /> as UTF-8, escaping each byte outside a valid sequence.</summary>
    /// <param name="bytes">The raw bytes.</param>
    /// <returns>The text, with one lone surrogate per invalid byte.</returns>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        StringBuilder text = new(bytes.Length);
        while (!bytes.IsEmpty)
        {
            if (Rune.DecodeFromUtf8(bytes, out Rune rune, out int consumed) == OperationStatus.Done)
            {
                text.Append(rune.ToString());
                bytes = bytes[consumed..];
            }
            else
            {
                text.Append((char)(SurrogateOffset + bytes[0]));
                bytes = bytes[1..];
            }
        }

        return text.ToString();
    }

    /// <summary>Encodes <paramref name="text" /> as UTF-8, giving each escaping surrogate back its byte.</summary>
    /// <param name="text">Text, possibly holding escaping surrogates from <see cref="Decode" />.</param>
    /// <returns>The bytes, with no terminating NUL.</returns>
    public static byte[] Encode(ReadOnlySpan<char> text)
    {
        ArrayBufferWriter<byte> bytes = new(text.Length + 1);
        Span<byte> utf8 = stackalloc byte[4];
        while (!text.IsEmpty)
        {
            Rune.DecodeFromUtf16(text, out Rune rune, out int consumed);
            if (text[0] is >= FirstEscape and <= LastEscape)
            {
                bytes.Write([(byte)(text[0] - SurrogateOffset)]);
            }
            else
            {
                bytes.Write(utf8[..rune.EncodeToUtf8(utf8)]);
            }

            text = text[consumed..];
        }

        return bytes.WrittenSpan.ToArray();
    }

    /// <summary>Tells whether <paramref name="text" /> holds an escaping surrogate, U+DC80 to U+DCFF.</summary>
    /// <param name="text">Text.</param>
    /// <returns><see langword="true" /> when a byte outside valid UTF-8 is escaped in it.</returns>
    public static bool HoldsEscape(ReadOnlySpan<char> text) => text.IndexOfAnyInRange(FirstEscape, LastEscape) >= 0;

    /// <summary>The <see cref="Encoding" /> behind <see cref="SurrogateEscapeUtf8.Encoding" />.</summary>
    private sealed class EscapingEncoding() : Encoding(Utf8CodePage)
    {
        public override int GetByteCount(char[] chars, int index, int count) => Encode(chars.AsSpan(index, count)).Length;

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            byte[] encoded = Encode(chars.AsSpan(charIndex, charCount));
            encoded.CopyTo(bytes, byteIndex);
            return encoded.Length;
        }

        public override int GetCharCount(byte[] bytes, int index, int count) => Decode(bytes.AsSpan(index, count)).Length;

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            string decoded = Decode(bytes.AsSpan(byteIndex, byteCount));
            decoded.CopyTo(0, chars, charIndex, decoded.Length);
            return decoded.Length;
        }

        public override int GetMaxByteCount(int charCount) => (charCount + 1) * 3;

        public override int GetMaxCharCount(int byteCount) => byteCount + 1;
    }
}
