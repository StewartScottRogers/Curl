using System.Text;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.Strings;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1396.c</c>: <c>curl_easy_unescape</c> and <c>curl_easy_escape</c> over upstream's two tables, writing
/// <c>curl_easy_unescape test &lt;i&gt; DONE</c> and <c>curl_easy_escape test &lt;i&gt; DONE (&lt;out&gt;)</c> on stdout
/// for each row. A wrong length or wrong bytes is a failed check that the program carries on past; no handle or a
/// <see langword="null"/> answer aborts it. The result is the failure count (<c>UNITTEST_END</c>).
/// </summary>
public sealed class Lib1396 : ILibTest
{
    /// <summary>
    /// <c>list1</c>: the unescape rows, the input with its C terminator (Latin-1), the length handed over (0 for
    /// <c>strlen</c>) and the bytes expected.
    /// </summary>
    public static IReadOnlyList<(byte[] Input, int Length, byte[] Expected)> UnescapeRows { get; } =
    [
        (Latin1("%61"), 3, Text("a")),
        (Latin1("%61a"), 4, Text("aa")),
        (Latin1("%61b"), 4, Text("ab")),
        (Latin1("%6 1"), 4, Text("%6 1")),
        (Latin1("%61"), 1, Text("%")),
        (Latin1("%61"), 2, Text("%6")),
        (Latin1("%6%a"), 4, Text("%6%a")),
        (Latin1("%6a"), 0, Text("j")),
        (Latin1("%FF"), 0, [0xFF]),
        (Latin1("%FF%00%ff"), 9, [0xFF, 0x00, 0xFF]),
        (Latin1("%-2"), 0, Text("%-2")),
        (Latin1("%FG"), 0, Text("%FG")),
    ];

    /// <summary><c>list2</c>: the escape rows, as <see cref="UnescapeRows"/>, the expected text as bytes.</summary>
    public static IReadOnlyList<(byte[] Input, int Length, byte[] Expected)> EscapeRows { get; } =
    [
        (Latin1("a"), 1, Text("a")),
        (Latin1("/"), 1, Text("%2F")),
        (Latin1("a=b"), 3, Text("a%3Db")),
        (Latin1("a=b"), 0, Text("a%3Db")),
        (Latin1("a=b"), 1, Text("a")),
        (Latin1("a=b"), 2, Text("a%3D")),
        (Latin1("1/./0"), 5, Text("1%2F.%2F0")),
        (Latin1("-._~!#%&"), 0, Text("-._~%21%23%25%26")),
        (Latin1("a"), 2, Text("a%00")),
        (Latin1("aÿ\u0001g"), 4, Text("a%FF%01g")),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1396";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var check = new LibTestAssertions(context.StandardError);
        var steps = new LibTestSteps(context.StandardError);
        if (!steps.GlobalInit(context.Engine, CurlGlobalFlags.All, out CurlLibrary? library))
        {
            check.Fail("unit_setup() FAILURE");
            return LibTestResult.FromCurlCode(check.FailuresAsResult);
        }

        await using (library.ConfigureAwait(false))
        {
            await TestAsync(context, check, library, UnescapeRows, EscapeRows).ConfigureAwait(false);
        }

        return LibTestResult.FromCurlCode(check.FailuresAsResult);
    }

    /// <summary>The body after <c>unit_setup</c>: both tables through one handle, which <c>unit_stop</c> cleans up.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="check">Counts and reports failed checks.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="unescapeRows">The unescape rows, as <see cref="UnescapeRows"/>.</param>
    /// <param name="escapeRows">The escape rows, as <see cref="EscapeRows"/>.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(
        LibTestContext context,
        LibTestAssertions check,
        CurlLibrary library,
        IEnumerable<(byte[] Input, int Length, byte[] Expected)> unescapeRows,
        IEnumerable<(byte[] Input, int Length, byte[] Expected)> escapeRows)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(library);
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            _ = check.AbortUnless(false, "returned NULL!", "easy");
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            await using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            _ = Unescape(easy, check, output, unescapeRows) && Escape(check, output, escapeRows);
        }
    }

    // The unescape loop; false once a NULL answer aborts the program.
    private static bool Unescape(CurlEasy easy, LibTestAssertions check, StreamWriter output, IEnumerable<(byte[] Input, int Length, byte[] Expected)> rows)
    {
        int i = 0;
        foreach (var (input, length, expected) in rows)
        {
            byte[]? decoded = CurlStrings.Unescape(easy, CText(input), length, out int outputLength);
            if (decoded is null)
            {
                return check.AbortUnless(false, "returned NULL!", "out");
            }

            _ = check.FailUnless(outputLength == expected.Length, "wrong output length returned", "outlen == list1[i].outlen");
            _ = check.FailUnless(StartsWith(decoded, expected), "bad output data returned", "!memcmp(out, list1[i].out, list1[i].outlen)");
            output.Write($"curl_easy_unescape test {i++} DONE\n");
        }

        return true;
    }

    // The escape loop; false once a NULL answer aborts the program. The bytes go over the raw-byte form, since the
    // text form reads its input as UTF-8 and a row holds 0xFF.
    private static bool Escape(LibTestAssertions check, StreamWriter output, IEnumerable<(byte[] Input, int Length, byte[] Expected)> rows)
    {
        int i = 0;
        foreach (var (input, length, expected) in rows)
        {
            int taken = length == 0 ? Array.IndexOf(input, (byte)0) : length;
            string? encoded = taken < 0 || taken > input.Length ? null : CurlStrings.Escape(input.AsSpan(0, taken));
            if (encoded is null)
            {
                return check.AbortUnless(false, "returned NULL!", "out");
            }

            _ = check.FailUnless(encoded.Length == expected.Length, "wrong output length returned", "outlen == list2[i].outlen");
            _ = check.FailUnless(StartsWith(Encoding.Latin1.GetBytes(encoded), expected), "bad output data returned", "!memcmp(out, list2[i].out, list2[i].outlen)");
            output.Write($"curl_easy_escape test {i++} DONE ({encoded})\n");
        }

        return true;
    }

    private static bool StartsWith(byte[] actual, byte[] expected) => actual.AsSpan().StartsWith(expected);

    // The input up to its C terminator, as the text the handle form takes; every unescape row is ASCII.
    private static string CText(byte[] input) => Encoding.Latin1.GetString(input, 0, Array.IndexOf(input, (byte)0));

    // A C string literal: its Latin-1 bytes and the terminating NUL.
    private static byte[] Latin1(string text) => [.. Text(text), 0];

    // An expected answer's bytes, without a terminator.
    private static byte[] Text(string text) => Encoding.Latin1.GetBytes(text);
}
