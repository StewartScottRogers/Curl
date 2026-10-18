using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib583.c</c>: an SFTP upload of five bytes as the user in <c>libtest_arg2</c> with the key files in the third
/// and fourth arguments, added to a multi handle and removed straight after one <c>curl_multi_perform</c>, writing
/// each call before and after it on stderr. Fewer than three arguments answer <see cref="LibTestErrorCodes.MajorBad"/>;
/// a failing remove writes its code and answers <see cref="LibTestErrorCodes.Multi"/>.
/// </summary>
public sealed class Lib583 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib583";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Arguments.Count < 3
            ? Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad))
            : LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));
    }

    /// <summary>The body after <c>global_init</c>: <c>multi_init</c>, <c>easy_init</c>, the options, the perform and the early remove.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return;
            }

            await using (easy.ConfigureAwait(false))
            {
                if (steps.SetOptionsAndAddHandle(
                    easy,
                    [
                        (CurlOption.Userpwd, context.Argument2),
                        (CurlOption.SshPublicKeyfile, context.Argument3),
                        (CurlOption.SshPrivateKeyfile, context.Argument4),
                        (CurlOption.Upload, 1L),
                        (CurlOption.Verbose, 1L),
                        (CurlOption.Url, context.Url),
                        (CurlOption.Infilesize, 5L),
                    ],
                    multi))
                {
                    await PerformAndRemoveAsync(steps, easy, multi, context.StandardError, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>One <c>multi_perform</c>, then <c>curl_multi_remove_handle</c> with its outcome written.</summary>
    /// <param name="steps">The program's steps.</param>
    /// <param name="easy">The added handle.</param>
    /// <param name="multi">The multi handle.</param>
    /// <param name="error">The program's stderr.</param>
    /// <param name="cancellationToken">Stops the perform.</param>
    /// <returns>A task that completes once the handle is removed or the perform failed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask PerformAndRemoveAsync(LibTestSteps steps, CurlEasy easy, CurlMulti multi, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(multi);
        ArgumentNullException.ThrowIfNull(error);
        error.Write("curl_multi_perform()...\n");
        if (!(await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false)).GoesOn)
        {
            return;
        }

        error.Write("curl_multi_perform() succeeded\n");
        error.Write("curl_multi_remove_handle()...\n");
        CurlMultiCode code = multi.RemoveHandle(easy);
        if (code != CurlMultiCode.Ok)
        {
            error.Write($"curl_multi_remove_handle() failed, with code {(int)code}\n");
            steps.Result = LibTestErrorCodes.Multi;
        }
        else
        {
            error.Write("curl_multi_remove_handle() succeeded\n");
        }
    }
}
