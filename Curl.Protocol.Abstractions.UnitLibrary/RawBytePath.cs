using System.Buffers;
using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The text form of a file path that may hold bytes that are not valid UTF-8, so a path curl hands
/// to <c>open()</c> unchanged can reach <see cref="IFileSystem" /> as a <see cref="string" />
/// (CGR-114, BL-3899, ADR-0836). <see cref="FromBytes" /> reads the bytes as UTF-8 but keeps each
/// byte <c>b</c> outside a valid sequence as the lone surrogate U+DC00 + <c>b</c> (U+DC80 to
/// U+DCFF), and <see cref="ToBytes" /> gives each such surrogate back its byte, so the round trip
/// keeps every byte. A path of valid UTF-8 holds no such surrogate and is the plain text it always was.
/// </summary>
public static class RawBytePath
{
    /// <summary>The first raw byte's surrogate: U+DC00 + 0x80, since a byte below 0x80 is always valid.</summary>
    private const char FirstRawByte = '\uDC80';

    /// <summary>The last raw byte's surrogate, U+DC00 + 0xFF.</summary>
    private const char LastRawByte = '\uDCFF';

    /// <summary>The offset from a raw byte to its surrogate.</summary>
    private const int SurrogateOffset = 0xDC00;

    /// <summary>Reads <paramref name="bytes" /> as UTF-8, keeping each byte outside a valid sequence as its surrogate.</summary>
    /// <param name="bytes">A path's bytes, as curl would pass them to <c>open()</c>.</param>
    /// <returns>The path's text form, with one lone surrogate per byte that is not valid UTF-8.</returns>
    public static string FromBytes(ReadOnlySpan<byte> bytes)
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

    /// <summary>Tells whether <paramref name="path" /> holds a raw byte's surrogate, U+DC80 to U+DCFF.</summary>
    /// <param name="path">A path's text form.</param>
    /// <returns><see langword="true" /> when only its raw bytes name the file.</returns>
    public static bool HoldsRawByte(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.AsSpan().IndexOfAnyInRange(FirstRawByte, LastRawByte) >= 0;
    }

    /// <summary>
    /// Encodes <paramref name="path" /> as UTF-8, giving each raw byte's surrogate back its byte; any
    /// other lone surrogate encodes as U+FFFD, as <see cref="Encoding.UTF8" /> encodes it.
    /// </summary>
    /// <param name="path">A path's text form, possibly from <see cref="FromBytes" />.</param>
    /// <returns>The path's bytes, with no terminating NUL.</returns>
    public static byte[] ToBytes(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArrayBufferWriter<byte> bytes = new(path.Length + 1);
        Span<byte> utf8 = stackalloc byte[4];
        ReadOnlySpan<char> text = path;
        while (!text.IsEmpty)
        {
            Rune.DecodeFromUtf16(text, out Rune rune, out int consumed);
            if (text[0] is >= FirstRawByte and <= LastRawByte)
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
}
