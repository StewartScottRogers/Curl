using System.Buffers;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Reads the bytes of a header file (<c>-H @file</c>, <c>--proxy-header @file</c>,
/// <c>-F ...;headers=@file</c>) as text the request side encodes back into those same bytes, since
/// curl 8.22.0 sends a header file's bytes unchanged and never transcodes them (CGR-020, BL-3549).
/// </summary>
public static class HeaderFileText
{
    /// <summary>The first of the lone low surrogates U+DC80 to U+DCFF that each stand for one byte UTF-8 cannot read.</summary>
    private const int EscapedByteBase = 0xDC00;

    /// <summary>
    /// Gives <paramref name="contents"/> as text: read in <paramref name="wireEncoding"/> when that
    /// encodes the text back into <paramref name="contents"/> (the Windows ANSI code page the request
    /// side encodes in); otherwise read as UTF-8 with each byte UTF-8 cannot read as the lone surrogate
    /// U+DC00 plus the byte, which the request side writes back as that byte.
    /// </summary>
    /// <param name="contents">The file's bytes.</param>
    /// <param name="wireEncoding">The encoding the request side sends header text in, or <see langword="null"/> for UTF-8.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contents"/> is <see langword="null"/>.</exception>
    public static string Read(byte[] contents, Encoding? wireEncoding)
    {
        ArgumentNullException.ThrowIfNull(contents);

        string? respelled = wireEncoding?.GetString(contents);
        return respelled is not null && wireEncoding!.GetBytes(respelled).AsSpan().SequenceEqual(contents)
            ? respelled
            : ReadUtf8EscapingUnreadableBytes(contents);
    }

    private static string ReadUtf8EscapingUnreadableBytes(ReadOnlySpan<byte> contents)
    {
        StringBuilder text = new(contents.Length);
        while (!contents.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf8(contents, out Rune rune, out int consumed);
            if (status == OperationStatus.Done)
            {
                text.Append(rune.ToString());
            }
            else
            {
                foreach (byte unreadable in contents[..consumed])
                {
                    text.Append((char)(EscapedByteBase + unreadable));
                }
            }

            contents = contents[consumed..];
        }

        return text.ToString();
    }
}
