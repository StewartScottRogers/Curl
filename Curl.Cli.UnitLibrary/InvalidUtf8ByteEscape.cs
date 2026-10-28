using System.Buffers;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Keeps every byte of an argument losslessly when it is not valid UTF-8 (CGR-016, BL-3849, ADR-0909):
/// <see cref="Decode"/> reads the bytes as UTF-8 but turns each byte <c>b</c> that is not part of a valid
/// sequence into the lone surrogate U+DC00 + <c>b</c> (the "surrogate escape" scheme), and
/// <see cref="Encoding"/> turns each such surrogate back into its byte. curl never re-encodes its
/// arguments, so <c>-d $'\xff'</c> posts the byte FF on Linux and macOS, where .NET would otherwise
/// hand over U+FFFD and send EF BF BD.
/// </summary>
public static class InvalidUtf8ByteEscape
{
    /// <summary>The first escaped byte's surrogate: U+DC00 + 0x80, since a byte below 0x80 is always valid.</summary>
    private const char FirstEscape = '\uDC80';

    /// <summary>The last escaped byte's surrogate, U+DC00 + 0xFF.</summary>
    private const char LastEscape = '\uDCFF';

    /// <summary>
    /// UTF-8 that encodes each lone surrogate U+DC80 to U+DCFF as the byte it escapes, and decodes as
    /// <see cref="Decode"/> does; any other lone surrogate encodes as U+FFFD, as <see cref="System.Text.Encoding.UTF8"/> does.
    /// </summary>
    public static Encoding Encoding { get; } = new EscapingUtf8Encoding();

    /// <summary>Reads <paramref name="bytes"/> as UTF-8, escaping each byte outside a valid sequence.</summary>
    /// <param name="bytes">An argument's raw bytes.</param>
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
                text.Append((char)(0xDC00 + bytes[0]));
                bytes = bytes[1..];
            }
        }

        return text.ToString();
    }

    /// <summary>Encodes <paramref name="text"/> as UTF-8, giving each escaping surrogate back its byte.</summary>
    /// <param name="text">Text, possibly holding escaping surrogates from <see cref="Decode"/>.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Encode(ReadOnlySpan<char> text)
    {
        ArrayBufferWriter<byte> bytes = new(text.Length + 1);
        Span<byte> utf8 = stackalloc byte[4];
        while (!text.IsEmpty)
        {
            Rune.DecodeFromUtf16(text, out Rune rune, out int consumed);
            if (text[0] is >= FirstEscape and <= LastEscape)
            {
                bytes.Write([(byte)(text[0] - 0xDC00)]);
            }
            else
            {
                bytes.Write(utf8[..rune.EncodeToUtf8(utf8)]);
            }

            text = text[consumed..];
        }

        return bytes.WrittenSpan.ToArray();
    }

    /// <summary>The <see cref="Encoding"/> behind <see cref="InvalidUtf8ByteEscape.Encoding"/>.</summary>
    private sealed class EscapingUtf8Encoding : Encoding
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
