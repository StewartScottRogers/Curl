using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib539.c</c>: a verbose FTP transfer of the case's URL with <c>CURLFTPMETHOD_SINGLECWD</c>, then, when it
/// succeeds, one of <c>libtest_arg2</c> on the same handle with <c>CURLFTPMETHOD_NOCWD</c> and the quote command
/// <c>SYST</c>. The program answers the last transfer's code.
/// </summary>
public sealed class Lib539 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib539";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            bool firstSucceeded = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.FtpFilemethod, (long)FtpFileMethod.SingleCwd),
                ],
                cancellationToken).ConfigureAwait(false);
            if (!firstSucceeded)
            {
                return;
            }

            CurlSlist commands = CurlSlist.Append(null, "SYST");
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Url, context.Argument2),
                    (CurlOption.FtpFilemethod, (long)FtpFileMethod.NoCwd),
                    (CurlOption.Quote, commands),
                ],
                cancellationToken).ConfigureAwait(false);
            CurlSlist.FreeAll(commands);
        }
    }
}
