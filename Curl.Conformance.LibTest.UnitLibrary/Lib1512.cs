using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1512.c</c>: writes the resolve entry <c>server.example.curl:&lt;port&gt;:&lt;address&gt;</c>
/// (<c>libtest_arg3</c>, <c>libtest_arg2</c>) on stdout, then makes two verbose easy handles with headers and
/// <c>CURLOPT_DNS_USE_GLOBAL_CACHE</c>, each to <c>http://server.example.curl:&lt;port&gt;/path/1512&lt;nnnn&gt;</c>,
/// gives only the first the entry (<c>CURLOPT_RESOLVE</c>) and performs them in turn, stopping at the first that fails.
/// A failing <c>curl_global_init</c> writes <c>curl_global_init() failed</c> and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>. The <c>easy_setopt</c> checks are left out: Libcurl takes these values
/// without fail.
/// </summary>
public sealed class Lib1512 : ILibTest
{
    /// <summary>The handles the program makes.</summary>
    public const int HandleCount = 2;

    /// <inheritdoc/>
    public string ProgramName => "lib1512";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        string entry = $"server.example.curl:{context.Argument3}:{context.Argument2}";
        using (StreamWriter output = LibTestRun.StandardOutputWriter(context))
        {
            output.Write($"{entry}\n");
        }

        CurlSlist resolve = CurlSlist.Append(null, entry);
        if (!steps.EasyInit(library, out CurlEasy? first))
        {
            return;
        }

        await using (first.ConfigureAwait(false))
        {
            SetOptions(context, first, 1);
            if (!steps.EasyInit(library, out CurlEasy? second))
            {
                return;
            }

            await using (second.ConfigureAwait(false))
            {
                SetOptions(context, second, 2);
                _ = first.SetOption(CurlOption.Resolve, resolve);
                _ = await steps.EasyPerformAsync(first, cancellationToken).ConfigureAwait(false)
                    && await steps.EasyPerformAsync(second, cancellationToken).ConfigureAwait(false);
            }
        }

        CurlSlist.FreeAll(resolve);
    }

    /// <summary>The URL of handle <paramref name="n"/>: <c>http://server.example.curl:&lt;port&gt;/path/1512&lt;nnnn&gt;</c>.</summary>
    /// <param name="context">The program's context: its <c>libtest_arg3</c>.</param>
    /// <param name="n">The handle's number, from 1.</param>
    /// <returns>The URL.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static string Url(LibTestContext context, int n)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"http://server.example.curl:{context.Argument3}/path/1512{n:D4}";
    }

    private static void SetOptions(LibTestContext context, CurlEasy easy, int n)
    {
        _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
        _ = easy.SetOption(CurlOption.Url, Url(context, n));
        _ = easy.SetOption(CurlOption.Verbose, 1L);
        _ = easy.SetOption(CurlOption.Header, 1L);
        _ = easy.SetOption(CurlOption.DnsUseGlobalCache, 1L);
    }
}
