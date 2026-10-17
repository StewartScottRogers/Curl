using System.Text;
using Libcurl.Global;
using Libcurl.Misc.Strings;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1537.c</c>: <c>curl_easy_escape</c> and the deprecated <c>curl_escape</c> of fourteen reserved and high bytes,
/// <c>curl_easy_unescape</c> and the deprecated <c>curl_unescape</c> back to them, and both functions given a length
/// of -1, which answer <c>NULL</c>, printed as <c>(nil)</c>, leaving the unescape's output length as it was (2017).
/// Each result is a line on stdout. The bytes are high, so both escapes take them as bytes
/// (<see cref="CurlStrings.Escape(ReadOnlySpan{byte})"/>); <c>curl_unescape</c>'s length is the decoded bytes up to
/// their first <c>NUL</c>, as upstream's <c>strlen</c> measures it.
/// </summary>
public sealed class Lib1537 : ILibTest
{
    /// <summary>The program's fourteen bytes, <c>a[]</c>.</summary>
    public static ReadOnlySpan<byte> Bytes => [0x2f, 0x3a, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f, 0x91, 0xa2, 0xb3, 0xc4, 0xd5, 0xe6, 0xf7];

    /// <summary>The output length the program sets before the -1-length unescape, which must leave it so.</summary>
    public const int ArbitraryLength = 2017;

    /// <inheritdoc/>
    public string ProgramName => "lib1537";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (_, _) => Test(context));

    /// <summary>The program's body after <c>curl_global_init</c>; the escapes do not fail on this input.</summary>
    /// <param name="context">The program's context.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static ValueTask Test(LibTestContext context)
    {
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        output.WriteLine(CurlStrings.Escape(Bytes));
        string escaped = CurlStrings.Escape(Bytes);
        output.WriteLine(escaped);

        byte[] raw = CurlStrings.Unescape(null, escaped, escaped.Length, out int outputLength)!;
        output.WriteLine($"outlen == {outputLength}");
        output.WriteLine($"unescape == original? {SameAsOriginal(raw, outputLength)}");

        raw = CurlStrings.Unescape(Encoding.ASCII.GetBytes(escaped));
        outputLength = LengthToNul(raw);
        output.WriteLine($"[old] outlen == {outputLength}");
        output.WriteLine($"[old] unescape == original? {SameAsOriginal(raw, outputLength)}");

        output.WriteLine($"escape -1 length: {Nil(CurlStrings.Escape(null, Encoding.Latin1.GetString(Bytes), -1))}");

        (string? unescaped, outputLength) = UnescapeKeepingLength("moahahaha", -1, ArbitraryLength);
        output.WriteLine($"unescape -1 length: {Nil(unescaped)} {outputLength}");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// <c>curl_easy_unescape(NULL, text, length, &amp;outlen)</c> as C sees it: the decoded text (Latin-1), and
    /// <c>outlen</c> set to its length, or left at <paramref name="outputLength"/> when the call answers <c>NULL</c>.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <param name="length">How many of its bytes to decode; 0 means all, a negative length answers <c>NULL</c>.</param>
    /// <param name="outputLength">The <c>outlen</c> before the call.</param>
    /// <returns>The decoded text, or <see langword="null"/>, and <c>outlen</c> after the call.</returns>
    public static (string? Text, int OutputLength) UnescapeKeepingLength(string text, int length, int outputLength)
    {
        byte[]? unescaped = CurlStrings.Unescape(null, text, length, out int written);
        return unescaped is null ? (null, outputLength) : (Encoding.Latin1.GetString(unescaped), written);
    }

    /// <summary>The <c>memcmp(raw, a, outlen) ? "no" : "YES"</c> answer.</summary>
    /// <param name="raw">The decoded bytes.</param>
    /// <param name="length">How many bytes to compare.</param>
    /// <returns><c>YES</c> when the first <paramref name="length"/> bytes match <see cref="Bytes"/>, else <c>no</c>.</returns>
    public static string SameAsOriginal(byte[] raw, int length) =>
        length <= raw.Length && length <= Bytes.Length && raw.AsSpan(0, length).SequenceEqual(Bytes[..length]) ? "YES" : "no";

    /// <summary><c>strlen</c> of decoded bytes: the count before the first <c>NUL</c>, or all of them.</summary>
    /// <param name="raw">The decoded bytes.</param>
    /// <returns>The length.</returns>
    public static int LengthToNul(byte[] raw)
    {
        int nul = Array.IndexOf(raw, (byte)0);
        return nul < 0 ? raw.Length : nul;
    }

    /// <summary><c>curl_mprintf</c>'s <c>%s</c>: the text, or <c>(nil)</c> for <c>NULL</c>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>What <c>%s</c> prints.</returns>
    public static string Nil(string? text) => text ?? "(nil)";
}
