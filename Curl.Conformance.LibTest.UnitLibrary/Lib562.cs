using System.Globalization;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib562.c</c>: a verbose transfer of the case's FTP URL with <c>CURLOPT_PORT</c> set to the number
/// <c>libtest_arg2</c> gives (KNOWN_BUGS 59: the <c>;type=A</c> must survive the port). When the argument is
/// not a number up to 65535 the program answers <see cref="LibTestErrorCodes.MajorBad"/> before anything else,
/// and a failing <c>curl_global_init</c> or <c>curl_easy_init</c> answers it too.
/// </summary>
public sealed class Lib562 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib562";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryParsePort(context.Argument2, out long port))
        {
            return Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad));
        }

        return LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, port, cancellationToken));
    }

    /// <summary><c>curlx_str_number(&amp;libtest_arg2, &amp;port, 0xffff)</c>: leading decimal digits, at most 65535.</summary>
    /// <param name="text">The argument, or <see langword="null"/>.</param>
    /// <param name="port">The number.</param>
    /// <returns>Whether the text starts with a number in range.</returns>
    public static bool TryParsePort(string? text, out long port)
    {
        string digits = new((text ?? string.Empty).TakeWhile(char.IsAsciiDigit).ToArray());
        port = 0;
        return digits.Length > 0
            && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out port)
            && port <= 0xffff;
    }

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="port">The port to set.</param>
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
                [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Verbose, 1L), (CurlOption.Port, port), (CurlOption.Url, context.Url)],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
