using Curl.Api.Codes;
using Libcurl.Global;
using Libcurl.Misc.Strings;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1560.c</c>: the URL API test. <see cref="Lib1560Checks"/>' groups run in upstream's order over
/// <see cref="Lib1560Tables"/>, and the first group with an error ends the program with that group's number
/// (13 for <c>test_scheme_guess</c> down to 8 for <c>clear_url</c>); when every group passes, <c>success</c> goes
/// to stdout. The program makes no <c>curl_global_init</c>; the port takes its library from
/// <see cref="LibTestRun.WithLibraryAsync"/>, and a cleaned-up library, where <c>curl_url()</c> has no handle to
/// give, answers 13 as <c>test_scheme_guess</c> does when it gets none.
/// </summary>
/// <param name="isWindows">Whether upstream's <c>_WIN32</c> rows run: <see cref="OperatingSystem.IsWindows"/> in the program table.</param>
public sealed class Lib1560(bool isWindows) : ILibTest
{
    /// <summary><c>test_scheme_guess</c>'s answer, which the program also gives when it can make no handle.</summary>
    public const int SchemeGuessFailed = 13;

    /// <inheritdoc/>
    public string ProgramName => "lib1560";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, isWindows));

    /// <summary>The program's body.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="isWindows">Whether upstream's <c>_WIN32</c> rows run.</param>
    /// <returns>A task that completes when the program returns.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            steps.Result = (CurlCode)SchemeGuessFailed;
            return ValueTask.CompletedTask;
        }

        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        bool hasUtf8 = CurlStrings.GetEnv("CURL_TEST_HAVE_CODESET_UTF8") is not null;
        var checks = new Lib1560Checks(library, context.StandardError, output, isWindows, hasUtf8);
        steps.Result = (CurlCode)RunGroups(output, Groups(checks));
        return ValueTask.CompletedTask;
    }

    /// <summary><c>test_lib1560</c>'s groups in upstream's order, each over its table, with the number the program returns when it fails.</summary>
    /// <param name="checks">The checks the groups run.</param>
    /// <returns>The groups.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="checks"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<(Func<int> Check, int Failed)> Groups(Lib1560Checks checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        return
        [
            (() => checks.SchemeGuess(Lib1560Tables.SchemeGuesses), SchemeGuessFailed),
            (checks.ApiErrors, 12),
            (checks.UrlDup, 11),
            (() => checks.SetGetParts(Lib1560Tables.SetGetParts), 10),
            (() => checks.GetUrls(Lib1560Tables.GetUrls), 3),
            (checks.Huge, 9),
            (checks.GetNothing, 7),
            (checks.ScopeId, 6),
            (() => checks.Appends(Lib1560Tables.Appends), 5),
            (() => checks.SetUrls(Lib1560Tables.SetUrls), 1),
            (() => checks.SetParts(Lib1560Tables.SetParts), 2),
            (() => checks.GetParts(Lib1560Tables.GetParts), 4),
            (() => checks.ClearUrl(Lib1560Tables.ClearUrls), 8),
        ];
    }

    /// <summary>Runs <paramref name="groups"/> in order until one reports an error, writing <c>success</c> when none does.</summary>
    /// <param name="output">The program's stdout.</param>
    /// <param name="groups">Each group with the number the program returns when it fails.</param>
    /// <returns>The first failing group's number, or 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static int RunGroups(TextWriter output, IEnumerable<(Func<int> Check, int Failed)> groups)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(groups);
        foreach ((Func<int> check, int failed) in groups)
        {
            if (check() != 0)
            {
                return failed;
            }
        }

        output.Write("success\n");
        return 0;
    }
}
