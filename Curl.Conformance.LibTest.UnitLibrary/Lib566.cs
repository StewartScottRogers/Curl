using System.Globalization;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib566.c</c>: one transfer of the case's URL with headers on; when it succeeds,
/// <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD</c> is written as <c>CL %.0f</c> to the file <c>libtest_arg2</c> names
/// (nothing when it cannot be opened). The program answers the transfer's code, else the getinfo's.
/// </summary>
public sealed class Lib566 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib566";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
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
            bool performed = await steps.SetOptionsAndPerformAsync(
                easy,
                [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Header, 1L)],
                cancellationToken).ConfigureAwait(false);
            if (performed)
            {
                steps.Result = easy.GetInfo(CurlInfo.ContentLengthDownload, out object? length);
                await WriteContentLengthAsync(context.Argument2, length as double? ?? 3, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Writes <c>CL %.0f</c> and a line feed to <paramref name="path"/>, doing nothing when it cannot be opened.</summary>
    /// <param name="path">The file, or <see langword="null"/>.</param>
    /// <param name="contentLength">The content length.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes when the file is written or skipped.</returns>
    public static async ValueTask WriteContentLengthAsync(string? path, double contentLength, CancellationToken cancellationToken)
    {
        string text = $"CL {contentLength.ToString("F0", CultureInfo.InvariantCulture)}\n";
        try
        {
            await File.WriteAllTextAsync(path!, text, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // curlx_fopen failed: upstream writes nothing.
        }
    }
}
