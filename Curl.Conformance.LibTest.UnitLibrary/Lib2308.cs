using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2308.c</c>: a transfer of the case's URL whose write callback answers <c>CURL_WRITEFUNC_ERROR</c>, then
/// <c>Returned N, should be 23.</c> on stdout. The program ends with <c>CURLE_OK</c> whatever the transfer gave.
/// </summary>
public sealed class Lib2308 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib2308";

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
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)FailWrite);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            await WriteReturnedAsync(context, easy, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>cb_curl</c> and <c>cb_ignore</c>: every chunk fails the transfer.</summary>
    /// <param name="data">The chunk, ignored.</param>
    /// <returns><c>CURL_WRITEFUNC_ERROR</c>.</returns>
    public static long FailWrite(ReadOnlySpan<byte> data) => WriteCallbackResult.Error;

    /// <summary>
    /// <c>curl_easy_perform</c> then <c>Returned N, should be 23.</c> on stdout, as lib2308 and lib2309 write it.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="easy">The handle to perform.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the line is written.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask WriteReturnedAsync(LibTestContext context, CurlEasy easy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(easy);
        CurlCode code = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        context.StandardOutput.Write(System.Text.Encoding.UTF8.GetBytes($"Returned {(int)code}, should be {(int)CurlCode.WriteError}.\n"));
    }
}
