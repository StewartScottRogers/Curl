using System.Globalization;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1558.c</c>: a transfer of the case's URL, then <c>CURLINFO_PROTOCOL</c> written on stdout in hex as
/// <c>Protocol: N</c> (case 1558's <c>file://</c> transfer gives 400, <c>CURLPROTO_FILE</c>). A failing perform
/// writes <c>curl_easy_perform() returned N (TEXT)</c> on stderr
/// and ends with its code.
/// </summary>
public sealed class Lib1558 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1558";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = steps.EasySetOptions(easy, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url)]);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (Failed(context, steps.Result, "curl_easy_perform"))
            {
                return;
            }

            // CURLINFO_PROTOCOL cannot fail on a live handle in Libcurl, so its curl_easy_getinfo() line is left out.
            _ = easy.GetInfo(CurlInfo.Protocol, out object? protocol);
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write($"Protocol: {Convert.ToInt64(protocol, CultureInfo.InvariantCulture):x}\n");
        }
    }

    private static bool Failed(LibTestContext context, CurlCode code, string call)
    {
        if (code == CurlCode.Ok)
        {
            return false;
        }

        context.StandardError.Write($"{call}() returned {(int)code} ({CurlErrorText.Easy(code)})\n");
        return true;
    }
}
