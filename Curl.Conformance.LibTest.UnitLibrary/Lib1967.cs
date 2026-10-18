using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Global;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1967.c</c>: a <c>CURLU</c> set to <c>a.b</c> with <c>CURLU_GUESS_SCHEME</c>, then to the relative <c>/x</c>
/// with <c>CURLU_NO_GUESS_SCHEME</c>, and, when <c>curl_url_get</c> answers, its URL written as <c>URL &lt;url&gt;</c>
/// on stdout: the guessed scheme must not survive as a guess. Upstream calls <c>curl_url()</c> without
/// <c>curl_global_init</c> and does nothing when it answers <c>NULL</c>, which a cleaned-up library stands for here. The
/// program always succeeds.
/// </summary>
public sealed class Lib1967 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1967";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (_, library) => TestAsync(context, library));

    /// <summary>The program's body.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="library">The library <c>curl_url()</c> is asked of.</param>
    /// <returns>A task that completes when the program returns.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ValueTask TestAsync(LibTestContext context, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            return ValueTask.CompletedTask;
        }

        using CurlUrlHandle url = library.UrlInit();
        _ = url.Set(CurlUrlPart.Url, "a.b", CurlUrlFlags.GuessScheme);
        _ = url.Set(CurlUrlPart.Url, "/x", CurlUrlFlags.NoGuessScheme);
        if (url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? text) == CurlUrlCode.Ok)
        {
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write($"URL {text}\n");
        }

        return ValueTask.CompletedTask;
    }
}
