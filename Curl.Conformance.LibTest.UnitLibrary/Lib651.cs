using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib651.c</c>: one copied <c>curl_formadd</c> field <c>hello</c> of more than 16K - 1000 <c>A</c>s, 1000
/// <c>B</c>s and so on to <c>Q</c>, less the last byte - posted verbose with headers. A failing
/// <c>curl_formadd</c> is printed to stdout and the post goes on.
/// </summary>
#pragma warning disable CS0618 // curl_formadd is deprecated upstream too; lib651 exercises it.
public sealed class Lib651 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib651";

    /// <summary><c>testbuf</c> as a string: 17 runs of 1000 letters from <c>A</c>, ending before its terminator.</summary>
    public static string TestBuffer { get; } = string.Concat(Enumerable.Range(0, 17).Select(i => new string((char)('A' + i), 1000)))[..16999];

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: adds the field, then posts the form.</summary>
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
        CurlForm? form = null;
        CurlFormAddCode code = CurlForm.Add(library, ref form, new CurlFormArgument(CurlFormOption.CopyName, "hello"), new CurlFormArgument(CurlFormOption.CopyContents, TestBuffer));
        if (code != CurlFormAddCode.Ok)
        {
            context.StandardOutput.Write(Encoding.ASCII.GetBytes($"curl_formadd(1) = {(int)code}\n"));
        }

        if (steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            await using (easy.ConfigureAwait(false))
            {
                _ = await steps.SetOptionsAndPerformAsync(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Url, context.Url),
                        (CurlOption.Httppost, form),
                        (CurlOption.Verbose, 1L),
                        (CurlOption.Header, 1L),
                    ],
                    cancellationToken).ConfigureAwait(false);
            }
        }

        CurlForm.Free(form);
    }
}
