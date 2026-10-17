using System.Text;
using Libcurl.Misc.Printf;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1398.c</c>: <c>curl_msnprintf</c> with precision, width, truncation, positional arguments and its limits
/// of 128 input arguments and 128 output segments. Each wrong answer or wrong text is a failed check the program
/// carries on past; the result is the failure count (<c>UNITTEST_END_SIMPLE</c>). It never calls
/// <c>curl_global_init</c>.
/// </summary>
public sealed class Lib1398 : ILibTest
{
    private const string WrongOutput = "wrong output";

    /// <summary>
    /// The program's calls in order: the buffer size, the format, its arguments, the answer expected with its
    /// message, and the text expected (<see langword="null"/> where upstream checks only the answer).
    /// </summary>
    public static IReadOnlyList<(int Size, string Format, object?[] Arguments, int Answer, string AnswerMessage, string? Text)> Calls { get; } =
    [
        (130, "%.*s", [-1, "bug"], 3, "return code should be 3", "bug"),
        (130, "%.*s", [-1, "0123456789"], 10, "return code should be 10", "0123456789"),
        (130, "%.*s", [0, "0123456789"], 0, "return code should be 0", string.Empty),
        (130, "%.*s", [-2, "bug"], 3, "return code should be 3", "bug"),
        (130, "%.*d", [-3, 10000], 5, "return code should be 5", "10000"),
        (130, "%.*d", [0, 1234567], 7, "return code should be 0", "1234567"),
        (4, "%.*s", [3, "bug"], 3, "return code should be 3", "bug"),
        (4, "%.*s", [3, "bug"], 3, "return code should be 3", "bug"),
        (4, "%.*s", [2, "bug"], 2, "return code should be 2", "bu"),
        (8, "%.8s", ["bug"], 3, "return code should be 3", "bug"),
        (8, "%.3s", ["0123456789"], 3, "return code should be 3", "012"),
        (8, "%-8s", ["bug"], 7, "return code should be 7", "bug    "),
        (8, "%8s", ["bug"], 7, "return code should be 7", "     bu"),
        (4, "%d", [10240], 3, "return code should be 3", "102"),
        (16, "%8s%8s", ["bug", "bug"], 15, "return code should be 15", "     bug     bu"),
        (16, "%8d%8d", [1234, 5678], 15, "return code should be 15", "    1234    567"),
        (24, "%2$.*1$.99d", [3, 5678], 0, "return code should be 0", null),
        (130, string.Concat(Enumerable.Repeat("%s", 129)), Strings(129), 0, "return code should be 0", null),
        (130, string.Concat(Enumerable.Repeat("%s", 128)), Strings(128), 13, "return code should be 13", null),
        (130, string.Concat(Enumerable.Repeat("%%", 129)), [], 0, "return code should be 0", null),
        (129, string.Concat(Enumerable.Repeat("%%", 128)), [], 128, "return code should be 128", null),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1398";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var check = new LibTestAssertions(context.StandardError);
        Test(check, Calls);
        return Task.FromResult(LibTestResult.FromCurlCode(check.FailuresAsResult));
    }

    /// <summary>Makes each call in <paramref name="calls"/> and checks its answer and text.</summary>
    /// <param name="check">Counts and reports failed checks.</param>
    /// <param name="calls">The calls, as <see cref="Calls"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Test(LibTestAssertions check, IEnumerable<(int Size, string Format, object?[] Arguments, int Answer, string AnswerMessage, string? Text)> calls)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(calls);
        foreach (var (size, format, arguments, answer, answerMessage, text) in calls)
        {
            byte[] output = new byte[size];
            int rc = CurlPrintf.MSNPrintf(output, format, arguments);
            _ = check.FailUnless(rc == answer, answerMessage, $"rc == {answer}");
            _ = text is null || check.FailUnless(CText(output) == text, WrongOutput, $"!strcmp(output, \"{text}\")");
        }
    }

    // The 129 and 128 input arguments: a letter at every tenth, a to m, empty strings between.
    private static object?[] Strings(int count) => [.. Enumerable.Range(0, count).Select(i => i % 10 == 0 ? ((char)('a' + (i / 10))).ToString() : string.Empty)];

    // The buffer up to its NUL, which curl_msnprintf always writes into a buffer of one byte or more.
    private static string CText(byte[] output) => Encoding.ASCII.GetString(output, 0, Array.IndexOf(output, (byte)0));
}
