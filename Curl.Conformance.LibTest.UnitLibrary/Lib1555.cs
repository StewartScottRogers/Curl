using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1555.c</c>: a GET whose progress callback calls <c>curl_easy_recv</c> and <c>curl_easy_send</c> on its own
/// handle, writing <c>curl_easy_recv returned N</c> and <c>curl_easy_send returned N</c> on stdout (case 1555 expects
/// 93, <c>CURLE_RECURSIVE_API_CALL</c>), then aborts the transfer. The perform's code is the program's.
/// </summary>
public sealed class Lib1555 : ILibTest
{
    /// <summary>The size of the callback's receive buffer.</summary>
    public const int BufferSize = 256;

    /// <inheritdoc/>
    public string ProgramName => "lib1555";

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
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Timeout, 7L),
                    (CurlOption.Nosignal, 1L),
                    (CurlOption.Progressfunction, (ProgressCallback)((_, _, _, _) => RecvAndSend(context, easy))),
                    (CurlOption.Progressdata, null),
                    (CurlOption.Noprogress, 0L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <c>progressCallback</c>: <c>curl_easy_recv</c> into a <see cref="BufferSize"/>-byte buffer and
    /// <c>curl_easy_send</c> of what it received, each answer written on stdout.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="easy">The transferring handle.</param>
    /// <returns>1, which aborts the transfer.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static int RecvAndSend(LibTestContext context, CurlEasy easy)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(easy);
        byte[] buffer = new byte[BufferSize];
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        var received = easy.Recv(buffer, out int count);
        output.Write($"curl_easy_recv returned {(int)received}\n");
        var sent = easy.Send(buffer.AsMemory(0, count), out _);
        output.Write($"curl_easy_send returned {(int)sent}\n");
        return 1;
    }
}
