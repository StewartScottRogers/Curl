using Curl.Api.Codes;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3101.c</c>: a verbose transfer of the case's URL, headers and body on stdout, with any HTTP authentication as
/// <c>user</c>:<c>password</c> and redirects allowed to <c>https</c> only. A failing <c>easy_setopt</c> ends the program
/// with its code; a failed transfer ends it with <see cref="LibTestErrorCodes.MajorBad"/>. A failing
/// <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib3101 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib3101";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
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
            (CurlOption, object?)[] options =
            [
                (CurlOption.Headerdata, context.StandardOutput),
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Verbose, 1L),
                (CurlOption.Url, context.Url),
                (CurlOption.Httpauth, (long)HttpAuthenticationMethods.Any),
                (CurlOption.Username, "user"),
                (CurlOption.Password, "password"),
                (CurlOption.RedirProtocolsStr, "https"),
            ];
            // Libcurl takes these easy_setopt values without fail, so the macros' jumps to test_cleanup are left out.
            LibTestRun.SetOptionsUnchecked(easy, options);
            if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                steps.Result = LibTestErrorCodes.MajorBad;
            }
        }
    }
}
