using System.Runtime.CompilerServices;
using Curl.Api.Codes;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>unitcheck.h</c>'s <c>fail_unless</c> and <c>abort_unless</c> and the <c>unitfail</c> count they share, which
/// lib1301, lib1308, lib1396 and lib1398 check their answers with. A failed check writes its file, line, expression and
/// message on <c>stderr</c> and counts one failure.
/// </summary>
/// <param name="error">The program's <c>stderr</c>.</param>
public sealed class LibTestAssertions(TextWriter error)
{
    private readonly TextWriter error = error ?? throw new ArgumentNullException(nameof(error));

    /// <summary><c>unitfail</c>: how many checks failed.</summary>
    public int Failures { get; private set; }

    /// <summary><c>UNITTEST_END</c>'s <c>(CURLcode)unitfail</c>: the failure count as the program's result.</summary>
    public CurlCode FailuresAsResult => (CurlCode)Failures;

    /// <summary><c>fail_unless</c>: counts and reports <paramref name="condition"/> when it is false.</summary>
    /// <param name="condition">What must hold.</param>
    /// <param name="message">The failure's message.</param>
    /// <param name="expression">The condition's source text.</param>
    /// <param name="file">The checking file.</param>
    /// <param name="line">The checking line.</param>
    /// <returns><paramref name="condition"/>.</returns>
    public bool FailUnless(
        bool condition,
        string message,
        [CallerArgumentExpression(nameof(condition))] string expression = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0) =>
        condition || Fail($"{Path.GetFileName(file)}:{line} Assertion '{expression}' FAILED: {message}");

    /// <summary><c>abort_unless</c>: counts and reports <paramref name="condition"/> when it is false; the caller then aborts.</summary>
    /// <param name="condition">What must hold.</param>
    /// <param name="message">The failure's message.</param>
    /// <param name="expression">The condition's source text.</param>
    /// <param name="file">The checking file.</param>
    /// <param name="line">The checking line.</param>
    /// <returns><paramref name="condition"/>: on <see langword="false"/> the program goes to its cleanup.</returns>
    public bool AbortUnless(
        bool condition,
        string message,
        [CallerArgumentExpression(nameof(condition))] string expression = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0) =>
        condition || Fail($"{Path.GetFileName(file)}:{line} ABORT assertion '{expression}' failed: {message}");

    /// <summary><c>fail</c>: counts and reports a failure the program found itself.</summary>
    /// <param name="message">The failure's message.</param>
    /// <param name="file">The checking file.</param>
    /// <param name="line">The checking line.</param>
    public void Fail(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        Fail($"{Path.GetFileName(file)}:{line} test FAILED: '{message}'");

    private bool Fail(string text)
    {
        Failures++;
        error.Write($"{text}\n");
        return false;
    }
}
