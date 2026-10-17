using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options.Http;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1922.c</c>: a GET of <c>http://hsts.example.com:&lt;port&gt;/1922</c>, resolved to the case's server, with HSTS
/// enabled and the body thrown away, whose <c>Strict-Transport-Security</c> answer fills the handle's live HSTS cache;
/// then <c>First request: HSTS cache populated</c> on stdout, <c>curl_easy_duphandle</c>, and the copy pointed at
/// <c>http://hsts.example.com/1922</c> through the HTTP proxy on <c>libtest_arg4</c>'s port. The copied cache must
/// upgrade that URL to HTTPS, so the proxy's refused <c>CONNECT</c> ends it with <c>CURLE_COULDNT_CONNECT</c> and
/// <c>Dup effective URL: &lt;url&gt;</c> goes to stdout. A first perform that fails writes
/// <c>First perform failed: N (text)</c>, a copy answering anything else <c>Dup perform unexpected result: N (text)</c>,
/// on stderr; the last code is the program's. Without the host, the HTTP port and the proxy port as
/// <c>libtest_arg2</c> to <c>libtest_arg4</c> it writes its usage and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// Libcurl's <c>curl_slist_append</c> and <c>curl_easy_duphandle</c> do not fail and its setters cannot fail on these
/// values, so those checks are left out.
/// </summary>
public sealed class Lib1922 : ILibTest
{
    /// <summary>The usage line written when an argument is missing.</summary>
    public const string UsageLine = "Usage: lib1922 - <host> <httpport> <proxyport>\n";

    /// <inheritdoc/>
    public string ProgramName => "lib1922";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Argument2 is null || context.Argument3 is null || context.Argument4 is null)
        {
            context.StandardError.Write(UsageLine);
            return Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad));
        }

        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context, its arguments checked.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            _ = easy.SetOption(CurlOption.Resolve, CurlSlist.Append(null, $"hsts.example.com:{context.Argument3}:{context.Argument2}"));
            _ = easy.SetOption(CurlOption.Url, $"http://hsts.example.com:{context.Argument3}/1922");
            _ = easy.SetOption(CurlOption.HstsCtrl, (long)HstsControl.Enable);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (steps.Result != CurlCode.Ok)
            {
                WriteFailure(context, "First perform failed", steps.Result);
                return;
            }

            output.Write("First request: HSTS cache populated\n");
            CurlEasy dup = easy.DupHandle();
            await using (dup.ConfigureAwait(false))
            {
                await PerformCopyAsync(context, steps, dup, output, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // The copy through the proxy, which must end CURLE_COULDNT_CONNECT, and its effective URL.
    private static async ValueTask PerformCopyAsync(LibTestContext context, LibTestSteps steps, CurlEasy dup, StreamWriter output, CancellationToken cancellationToken)
    {
        _ = dup.SetOption(CurlOption.Url, "http://hsts.example.com/1922");
        _ = dup.SetOption(CurlOption.Proxy, $"http://{context.Argument2}:{context.Argument4}");
        steps.Result = await dup.PerformAsync(cancellationToken).ConfigureAwait(false);
        if (steps.Result != CurlCode.CouldntConnect)
        {
            WriteFailure(context, "Dup perform unexpected result", steps.Result);
            return;
        }

        _ = dup.GetInfo(CurlInfo.EffectiveUrl, out object? effective);
        if (effective is string url)
        {
            output.Write($"Dup effective URL: {url}\n");
        }
    }

    private static void WriteFailure(LibTestContext context, string what, CurlCode result) =>
        context.StandardError.Write($"{what}: {(int)result} ({CurlErrorText.Easy(result)})\n");
}
