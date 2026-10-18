using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3034.c</c>: a verbose upload of <see cref="Payload"/> to the case's URL, following redirects, with a read
/// callback and no seek callback, so the redirect's rewind must fail with <see cref="CurlCode.SendFailRewind"/>; then a
/// <c>curl_easy_reset</c> and a verbose transfer of the same URL, whose code is the program's. Any other first code
/// writes its failure line and is the program's.
/// </summary>
public sealed class Lib3034 : ILibTest
{
    /// <summary><c>data_3034</c> with its terminating NUL, which <c>sizeof</c> counts: what the read callback copies.</summary>
    public static ReadOnlySpan<byte> Payload => "hello\0"u8;

    /// <inheritdoc/>
    public string ProgramName => "lib3034";

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

        await using (easy.ConfigureAwait(false))
        {
            // Libcurl takes these easy_setopt values without fail, so the macros' jumps to test_cleanup are left out.
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
            _ = easy.SetOption(CurlOption.Upload, 1L);
            _ = easy.SetOption(CurlOption.Infilesize, 5L);
            _ = easy.SetOption(CurlOption.Readfunction, (ReadCallback)Read);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (steps.Result != CurlCode.SendFailRewind)
            {
                _ = Lib2700.ReportFailure(context.StandardError, "curl_easy_perform", steps.Result);
                return;
            }

            easy.Reset();
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t3034_read_cb</c>: copies as much of <see cref="Payload"/> as fits, every call.</summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <returns>The bytes copied.</returns>
    public static long Read(Span<byte> buffer)
    {
        int count = Math.Min(Payload.Length, buffer.Length);
        Payload[..count].CopyTo(buffer);
        return count;
    }
}
