using System.Text;
using Libcurl.Easy;

namespace Libcurl.Misc.Strings;

/// <summary>
/// libcurl's string functions (ADR-0595): <c>curl_easy_escape</c> and <c>curl_easy_unescape</c>
/// with their deprecated handle-less forms <c>curl_escape</c> and <c>curl_unescape</c>,
/// <c>curl_getenv</c>, <c>curl_strequal</c> and <c>curl_strnequal</c>, each answering as
/// curl 8.22.0's <c>lib/escape.c</c>, <c>lib/getenv.c</c> and <c>lib/strequal.c</c> do.
/// </summary>
/// <remarks>
/// libcurl works on bytes, so text is read as its UTF-8 bytes and a length counts those bytes.
/// Where libcurl returns <c>NULL</c> this answers <see langword="null"/>.
/// </remarks>
public static class CurlStrings
{
    private const string UppercaseHexDigits = "0123456789ABCDEF";

    /// <summary>
    /// <c>curl_easy_escape</c>: <paramref name="text"/> URL-encoded, every byte but the unreserved
    /// <c>A-Z a-z 0-9 - . _ ~</c> written as <c>%XX</c> in uppercase hex.
    /// </summary>
    /// <param name="handle">The easy handle; libcurl ignores it, and so does this.</param>
    /// <param name="text">The text, read as UTF-8 bytes.</param>
    /// <param name="length">How many of those bytes to encode; 0 means all of them.</param>
    /// <returns>
    /// The encoded text (empty for empty input), or <see langword="null"/> when
    /// <paramref name="text"/> is <see langword="null"/>, <paramref name="length"/> is negative, or
    /// <paramref name="length"/> is past the end of the bytes.
    /// </returns>
    public static string? Escape(CurlEasy? handle, string? text, int length) => EscapeBytes(SelectBytes(text, length));

    /// <summary><c>curl_escape</c>, deprecated upstream: <see cref="Escape(CurlEasy?, string?, int)"/> with no handle.</summary>
    /// <param name="text">The text, read as UTF-8 bytes.</param>
    /// <param name="length">How many of those bytes to encode; 0 means all of them.</param>
    /// <returns>The encoded text, or <see langword="null"/> as for the handle form.</returns>
    [Obsolete("curl_escape is deprecated: use CurlStrings.Escape with a handle (curl_easy_escape).")]
    public static string? Escape(string? text, int length) => Escape(null, text, length);

    /// <summary>
    /// <c>curl_easy_escape</c> over raw bytes: <paramref name="bytes"/> URL-encoded as
    /// <see cref="Escape(CurlEasy?, string?, int)"/> encodes text.
    /// </summary>
    /// <param name="bytes">The bytes to encode, all of them.</param>
    /// <returns>The encoded text; empty for no bytes.</returns>
    public static string Escape(ReadOnlySpan<byte> bytes)
    {
        var escaped = new StringBuilder(bytes.Length);
        foreach (byte value in bytes)
        {
            AppendEscaped(escaped, value);
        }

        return escaped.ToString();
    }

    /// <summary>
    /// <c>curl_easy_unescape</c>: <paramref name="text"/> URL-decoded, each <c>%</c> followed by two
    /// hex digits (either case) read as that byte, <c>%00</c> included, and every other byte,
    /// <c>+</c> and a lone <c>%</c> among them, kept as it is.
    /// </summary>
    /// <param name="handle">The easy handle; libcurl uses it only to report running out of memory, so this ignores it.</param>
    /// <param name="text">The text, read as UTF-8 bytes.</param>
    /// <param name="length">How many of those bytes to decode; 0 means all of them.</param>
    /// <param name="outputLength">The decoded byte count, 0 when this answers <see langword="null"/>.</param>
    /// <returns>
    /// The decoded bytes (empty for empty input), or <see langword="null"/> when
    /// <paramref name="text"/> is <see langword="null"/>, <paramref name="length"/> is negative, or
    /// <paramref name="length"/> is past the end of the bytes.
    /// </returns>
    public static byte[]? Unescape(CurlEasy? handle, string? text, int length, out int outputLength)
    {
        byte[]? decoded = UnescapeBytes(SelectBytes(text, length));
        outputLength = decoded?.Length ?? 0;
        return decoded;
    }

    /// <summary><c>curl_unescape</c>, deprecated upstream: <see cref="Unescape(CurlEasy?, string?, int, out int)"/> with no handle or length out.</summary>
    /// <param name="text">The text, read as UTF-8 bytes.</param>
    /// <param name="length">How many of those bytes to decode; 0 means all of them.</param>
    /// <returns>The decoded bytes, or <see langword="null"/> as for the handle form.</returns>
    [Obsolete("curl_unescape is deprecated: use CurlStrings.Unescape with a handle (curl_easy_unescape).")]
    public static byte[]? Unescape(string? text, int length) => Unescape(null, text, length, out _);

    /// <summary>
    /// <c>curl_easy_unescape</c> over raw bytes: <paramref name="bytes"/> URL-decoded as
    /// <see cref="Unescape(CurlEasy?, string?, int, out int)"/> decodes text.
    /// </summary>
    /// <param name="bytes">The bytes to decode, all of them.</param>
    /// <returns>The decoded bytes; empty for no bytes.</returns>
    public static byte[] Unescape(ReadOnlySpan<byte> bytes)
    {
        var decoded = new List<byte>(bytes.Length);
        int index = 0;
        while (index < bytes.Length)
        {
            if (IsEscapeAt(bytes, index))
            {
                decoded.Add((byte)((HexValue(bytes[index + 1]) << 4) | HexValue(bytes[index + 2])));
                index += 3;
            }
            else
            {
                decoded.Add(bytes[index]);
                index++;
            }
        }

        return [.. decoded];
    }

    /// <summary>
    /// <c>curl_getenv</c>: the value of the environment variable <paramref name="name"/>, or
    /// <see langword="null"/> when it is unset or empty, as libcurl answers on every platform.
    /// </summary>
    /// <param name="name">The variable's name.</param>
    /// <returns>Its value, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static string? GetEnv(string name) => GetEnv(name, Environment.GetEnvironmentVariable);

    /// <summary><see cref="GetEnv(string)"/> over the given variable lookup, so tests need not change the process's environment.</summary>
    internal static string? GetEnv(string name, Func<string, string?> lookUpVariable)
    {
        ArgumentNullException.ThrowIfNull(name);
        string? value = lookUpVariable(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// <c>curl_strequal</c>: whether <paramref name="first"/> and <paramref name="second"/> are equal
    /// ignoring the case of ASCII letters only, a <c>NUL</c> character ending either as in C. Two
    /// <see langword="null"/> strings are equal; one is equal to nothing. libcurl answers 1 or 0.
    /// </summary>
    /// <param name="first">The first string.</param>
    /// <param name="second">The second string.</param>
    /// <returns><see langword="true"/> where libcurl answers 1.</returns>
    public static bool StrEqual(string? first, string? second) => StrNEqual(first, second, nuint.MaxValue);

    /// <summary>
    /// <c>curl_strnequal</c>: whether the first <paramref name="maximum"/> characters of
    /// <paramref name="first"/> and <paramref name="second"/> are equal as <see cref="StrEqual"/>
    /// compares them. Any two strings are equal in 0 characters; two <see langword="null"/>
    /// strings are equal only when <paramref name="maximum"/> is not 0.
    /// </summary>
    /// <param name="first">The first string.</param>
    /// <param name="second">The second string.</param>
    /// <param name="maximum">How many characters to compare at most.</param>
    /// <returns><see langword="true"/> where libcurl answers 1.</returns>
    public static bool StrNEqual(string? first, string? second, nuint maximum)
    {
        if (first is null || second is null)
        {
            return first is null && second is null && maximum != 0;
        }

        return CompareIgnoringAsciiCase(first, second, maximum);
    }

    private static bool CompareIgnoringAsciiCase(string first, string second, nuint maximum)
    {
        int index = 0;
        while (maximum != 0 && CharAt(first, index) != '\0')
        {
            if (AsciiUpper(CharAt(first, index)) != AsciiUpper(CharAt(second, index)))
            {
                return false;
            }

            maximum--;
            index++;
        }

        return maximum == 0 || AsciiUpper(CharAt(first, index)) == AsciiUpper(CharAt(second, index));
    }

    private static char CharAt(string text, int index) => index < text.Length ? text[index] : '\0';

    private static char AsciiUpper(char character) => char.IsAsciiLetterLower(character) ? (char)(character - ('a' - 'A')) : character;

    private static byte[]? SelectBytes(string? text, int length)
    {
        if (text is null || length < 0)
        {
            return null;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        if (length == 0 || length == bytes.Length)
        {
            return bytes;
        }

        return length < bytes.Length ? bytes[..length] : null;
    }

    private static string? EscapeBytes(byte[]? bytes) => bytes is null ? null : Escape(bytes);

    private static byte[]? UnescapeBytes(byte[]? bytes) => bytes is null ? null : Unescape(bytes);

    private static void AppendEscaped(StringBuilder escaped, byte value)
    {
        if (IsUnreserved(value))
        {
            escaped.Append((char)value);
            return;
        }

        escaped.Append('%').Append(UppercaseHexDigits[value >> 4]).Append(UppercaseHexDigits[value & 0x0F]);
    }

    private static bool IsUnreserved(byte value) =>
        char.IsAsciiLetterOrDigit((char)value) || value is (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~';

    private static bool IsEscapeAt(ReadOnlySpan<byte> bytes, int index) =>
        bytes[index] == (byte)'%' && index + 2 < bytes.Length && char.IsAsciiHexDigit((char)bytes[index + 1]) && char.IsAsciiHexDigit((char)bytes[index + 2]);

    private static int HexValue(byte digit) => char.IsAsciiDigit((char)digit) ? digit - '0' : (digit | 0x20) - 'a' + 10;
}
