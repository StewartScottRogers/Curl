using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib521.c</c>: one verbose transfer of the case's URL to the port <c>libtest_arg2</c> names
/// (<c>CURLOPT_PORT</c>) as <c>xxx:yyy</c>. A port that does not parse answers
/// <see cref="LibTestErrorCodes.MajorBad"/> before <c>curl_global_init</c>.
/// </summary>
public sealed class Lib521 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib521";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        long? port = ParsePort(context.Argument2);
        return port is null
            ? Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad))
            : LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, port.Value, cancellationToken));
    }

    /// <summary>
    /// <c>curlx_str_number(&amp;libtest_arg2, &amp;port, 0xffff)</c>: the leading decimal digits of
    /// <paramref name="text"/> as a number of at most 65535.
    /// </summary>
    /// <param name="text">The argument, or <see langword="null"/>.</param>
    /// <returns>The port, or <see langword="null"/> when no digit leads or the number passes 65535.</returns>
    public static long? ParsePort(string? text)
    {
        long port = 0;
        int digits = 0;
        while (text is not null && digits < text.Length && char.IsAsciiDigit(text[digits]))
        {
            port = (port * 10) + (text[digits] - '0');
            digits++;
            if (port > 0xffff)
            {
                return null;
            }
        }

        return digits == 0 ? null : port;
    }

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="port">The parsed port.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, long port, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Port, port),
                    (CurlOption.Userpwd, "xxx:yyy"),
                    (CurlOption.Verbose, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
