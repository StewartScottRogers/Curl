using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3100.c</c>: a verbose RTSP <c>DESCRIBE</c> of the case's URL, its headers and body on stdout, with
/// <c>CURLAUTH_ANY</c> and the credentials <c>user</c> and <c>password</c>. A failing transfer writes
/// <c>Failed to send DESCRIBE: N</c> to stderr and ends with <see cref="LibTestErrorCodes.MajorBad"/>, as does a failing
/// <c>curl_global_init</c> or <c>curl_easy_init</c>.
/// </summary>
public sealed class Lib3100 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib3100";

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
            // Libcurl takes these easy_setopt values without fail, so the macros' jumps to test_cleanup are left out.
            _ = easy.SetOption(CurlOption.Headerdata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.RtspStreamUri, context.Url);
            _ = easy.SetOption(CurlOption.Httpauth, (long)HttpAuthenticationMethods.Any);
            _ = easy.SetOption(CurlOption.Username, "user");
            _ = easy.SetOption(CurlOption.Password, "password");
            _ = easy.SetOption(CurlOption.RtspRequest, (long)RtspRequest.Describe);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (steps.Result != CurlCode.Ok)
            {
                context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"Failed to send DESCRIBE: {(int)steps.Result}\n"));
                steps.Result = LibTestErrorCodes.MajorBad;
            }
        }
    }
}
