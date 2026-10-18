using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib766.c</c>: a verbose active FTP transfer (<c>CURLOPT_FTPPORT</c> <c>-</c>) whose socket-option callback refuses
/// the accepted data connection, writing <c>Return error from CURLOPT_SOCKOPTFUNCTION callback</c> on stderr, so the
/// transfer ends with <c>CURLE_ABORTED_BY_CALLBACK</c>, the program's result.
/// </summary>
public sealed class Lib766 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib766";

    /// <summary><c>sockopt_766</c>: refuses an accepted socket and lets every other one through.</summary>
    /// <param name="error">The program's stderr.</param>
    /// <returns>The callback.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static SocketOptionCallback SocketOptionCallbackFor(TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return (_, purpose) =>
        {
            if (purpose != CurlSocketType.Accept)
            {
                return SocketOptionResult.Ok;
            }

            error.Write("Return error from CURLOPT_SOCKOPTFUNCTION callback\n");
            return SocketOptionResult.Error;
        };
    }

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>res_global_init</c>: the one transfer, its code the program's result.</summary>
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
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Ftpport, "-"),
                    (CurlOption.Sockoptfunction, SocketOptionCallbackFor(context.StandardError)),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
