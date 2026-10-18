using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1985.c</c>: <c>firsthost</c> and <c>secondhost</c> both resolved to <c>libtest_arg4</c> on port
/// <c>libtest_arg3</c>; a verbose transfer of the case's URL with <c>alice</c>'s password and a bearer token under Basic or
/// Bearer, its body thrown away, then the same handle to <c>libtest_arg2</c> under Digest. Each transfer's code is written
/// as <c>STEP1_CODE=</c> and <c>STEP2_CODE=</c>; the second is the program's. The program sets its options unchecked and
/// does not check <c>curl_global_init</c>; a library that gives no handle skips both transfers and the program succeeds.
/// The <c>curl_slist_append</c> checks are left out: Libcurl's list append does not fail.
/// </summary>
public sealed class Lib1985 : ILibTest
{
    /// <summary>The credentials of both transfers.</summary>
    public const string UserPassword = "alice:correct-horse-battery-staple";

    /// <inheritdoc/>
    public string ProgramName => "lib1985";

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
        ArgumentNullException.ThrowIfNull(library);
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            CurlSlist resolves = CurlSlist.Append(
                CurlSlist.Append(null, $"firsthost:{context.Argument3}:{context.Argument4}"),
                $"secondhost:{context.Argument3}:{context.Argument4}");
            _ = easy.SetOption(CurlOption.Resolve, resolves);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            _ = easy.SetOption(CurlOption.Userpwd, UserPassword);
            _ = easy.SetOption(CurlOption.Xoauth2Bearer, "unused-token");
            _ = easy.SetOption(CurlOption.Httpauth, (long)(HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.Bearer));
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            CurlCode first = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            output.Write($"STEP1_CODE={(int)first}\n");

            _ = easy.SetOption(CurlOption.Url, context.Argument2);
            _ = easy.SetOption(CurlOption.Httpauth, (long)HttpAuthenticationMethods.Digest);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            output.Write($"STEP2_CODE={(int)steps.Result}\n");
        }
    }
}
