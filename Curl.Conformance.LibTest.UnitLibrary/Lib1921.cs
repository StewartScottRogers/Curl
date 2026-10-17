using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Global;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1921.c</c>: a <c>CURLU</c> handle with no scheme is given the host <c>example.com</c> and the path
/// <c>/original</c>, then the relative URL <c>/newpath</c> with <c>CURLU_DEFAULT_SCHEME</c>, which reaches the URL
/// parser's redirect with no scheme to keep; <c>URL: &lt;url&gt;</c> goes to stdout. The first failing call ends the
/// chain, and the program answers <c>CURLE_BAD_FUNCTION_ARGUMENT</c> for it, <c>CURLE_OK</c> otherwise, and
/// <c>CURLE_FAILED_INIT</c> when no handle can be created. The program makes no <c>curl_global_init</c>; the port
/// takes its library from <see cref="LibTestRun.WithLibraryAsync"/>, whose init does not fail on the case's engine.
/// </summary>
public sealed class Lib1921 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1921";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

    /// <summary>The program's body.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program returns.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            steps.Result = CurlCode.FailedInit;
            return ValueTask.CompletedTask;
        }

        using CurlUrlHandle url = library.UrlInit();
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        CurlUrlCode code = RunChain(
        [
            () => url.Set(CurlUrlPart.Host, "example.com", CurlUrlFlags.None),
            () => url.Set(CurlUrlPart.Path, "/original", CurlUrlFlags.None),
            () => url.Set(CurlUrlPart.Url, "/newpath", CurlUrlFlags.DefaultScheme),
            () => WriteUrl(url, output),
        ]);
        steps.Result = code == CurlUrlCode.Ok ? CurlCode.Ok : CurlCode.BadFunctionArgument;
        return ValueTask.CompletedTask;
    }

    /// <summary>Runs <paramref name="calls"/> in order until one fails (upstream's <c>if(!rc)</c> chain).</summary>
    /// <param name="calls">The calls.</param>
    /// <returns>The first failing call's code, or <see cref="CurlUrlCode.Ok"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="calls"/> is <see langword="null"/>.</exception>
    public static CurlUrlCode RunChain(IEnumerable<Func<CurlUrlCode>> calls)
    {
        ArgumentNullException.ThrowIfNull(calls);
        foreach (Func<CurlUrlCode> call in calls)
        {
            CurlUrlCode code = call();
            if (code != CurlUrlCode.Ok)
            {
                return code;
            }
        }

        return CurlUrlCode.Ok;
    }

    /// <summary><c>curl_url_get</c> of the whole URL and, when it succeeds, the <c>URL: </c> line.</summary>
    /// <param name="url">The handle.</param>
    /// <param name="output">The program's stdout.</param>
    /// <returns>The get's code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static CurlUrlCode WriteUrl(CurlUrlHandle url, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(output);
        CurlUrlCode code = url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? text);
        if (code == CurlUrlCode.Ok)
        {
            output.Write($"URL: {text}\n");
        }

        return code;
    }
}
