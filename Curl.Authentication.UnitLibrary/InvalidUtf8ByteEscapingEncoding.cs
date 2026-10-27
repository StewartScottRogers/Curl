using System.Buffers;
using System.Text;

namespace Curl.Authentication;

/// <summary>
/// UTF-8 that keeps every byte: decoding turns each byte <c>b</c> outside a valid UTF-8 sequence into
/// the lone surrogate U+DC00 + <c>b</c> (the "surrogate escape" scheme), and encoding turns each lone
/// surrogate U+DC80 to U+DCFF back into its byte; any other lone surrogate encodes as U+FFFD, as
/// <see cref="Encoding.UTF8" /> does, so valid text encodes exactly as UTF-8. It is the credential
/// encoding off Windows (<see cref="CredentialEncoding.ForPlatform" />), so a URL credential that
/// percent-decodes to bytes that are not UTF-8 (<c>http://user:pa%FFss@host/</c>) is sent as those
/// bytes, as curl 8.18.0 OpenSSL on Linux sends them (CGR-053, ADR-0822). It reports UTF-8's code page.
/// </summary>
internal sealed class InvalidUtf8ByteEscapingEncoding() : Encoding(Utf8CodePage)
{
    /// <summary>The code page of UTF-8.</summary>
    private const int Utf8CodePage = 65001;

    /// <summary>The first escaped byte's surrogate: U+DC00 + 0x80, since a byte below 0x80 is always valid.</summary>
    private const char FirstEscape = '\uDC80';

    /// <summary>The last escaped byte's surrogate, U+DC00 + 0xFF.</summary>
    private const char LastEscape = '\uDCFF';

    /// <summary>The one instance.</summary>
    public static InvalidUtf8ByteEscapingEncoding Instance { get; } = new();

    /// <inheritdoc />
    public override int GetByteCount(char[] chars, int index, int count) => Encode(chars.AsSpan(index, count)).Length;

    /// <inheritdoc />
    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        byte[] encoded = Encode(chars.AsSpan(charIndex, charCount));
        encoded.CopyTo(bytes, byteIndex);
        return encoded.Length;
    }

    /// <inheritdoc />
    public override int GetCharCount(byte[] bytes, int index, int count) => Decode(bytes.AsSpan(index, count)).Length;

    /// <inheritdoc />
    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        string decoded = Decode(bytes.AsSpan(byteIndex, byteCount));
        decoded.CopyTo(0, chars, charIndex, decoded.Length);
        return decoded.Length;
    }

    /// <inheritdoc />
    public override int GetMaxByteCount(int charCount) => (charCount + 1) * 3;

    /// <inheritdoc />
    public override int GetMaxCharCount(int byteCount) => byteCount + 1;

    /// <summary>Reads <paramref name="bytes" /> as UTF-8, escaping each byte outside a valid sequence.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text, with one lone surrogate per invalid byte.</returns>
    private static string Decode(ReadOnlySpan<byte> bytes)
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

    /// <summary>Encodes <paramref name="text" /> as UTF-8, giving each escaping surrogate back its byte.</summary>
    /// <param name="text">Text, possibly holding escaping surrogates.</param>
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
}
