using Curl.Api.EngineContracts.Mime;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

#pragma warning disable CS0618 // curl_formadd is deprecated upstream too; lib1545 exercises it.

/// <summary>
/// <c>lib1545.c</c>: a <c>curl_formadd</c> form whose one field <c>file</c> names the unreadable <c>missing-file</c>,
/// posted twice with <c>CURLOPT_HTTPPOST</c>, both transfers' codes ignored, so reusing the form after a failure must
/// not crash. The program ends with <c>CURLE_OK</c> unless <c>global_init</c>, <c>easy_init</c> or setting the URL fails.
/// </summary>
public sealed class Lib1545 : ILibTest
{
    /// <summary>The file the form's field names, which does not exist.</summary>
    public const string MissingFile = "missing-file";

    /// <inheritdoc/>
    public string ProgramName => "lib1545";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        CurlForm? form = null;
        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = CurlForm.Add(library, ref form, new CurlFormArgument(CurlFormOption.CopyName, "file"), new CurlFormArgument(CurlFormOption.File, MissingFile));
            _ = easy.SetOption(CurlOption.Httppost, form);
            _ = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            _ = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        }

        CurlForm.Free(form);
    }
}
