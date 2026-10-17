using System.Globalization;
using System.Text.RegularExpressions;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1589.c</c>: the case's URL through the proxy <c>libtest_arg2</c> on port <c>libtest_arg3</c> with Digest proxy
/// credentials from <c>libtest_arg5</c>, then, unless it failed, <c>lib1589: now we do the request again</c> on stderr
/// and the same handle through the same proxy on port <c>libtest_arg4</c>. Fewer than four arguments after the program
/// name end with <see cref="LibTestErrorCodes.MajorBad"/> before <c>curl_global_init</c>; the last perform's code is
/// the program's.
/// </summary>
public sealed class Lib1589 : ILibTest
{
    /// <summary>The line written on stderr between the two requests.</summary>
    public const string AgainLine = "lib1589: now we do the request again\n";

    /// <inheritdoc/>
    public string ProgramName => "lib1589";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Arguments.Count < 4
            ? Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad))
            : LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));
    }

    /// <summary>The program's body after <c>res_global_init</c>.</summary>
    /// <param name="context">The program's context, with at least four arguments.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
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
            if (!await RunAsync(context, steps, easy, ParseInteger(context.Argument3), cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            context.StandardError.Write(AgainLine);
            _ = await RunAsync(context, steps, easy, ParseInteger(context.Argument4), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// C's <c>atoi</c>: an optional sign and the leading decimal digits after any leading white space, <c>0</c> when there
    /// are none (or no string).
    /// </summary>
    /// <param name="text">The text to read, or <see langword="null"/>.</param>
    /// <returns>The number the text starts with.</returns>
    public static long ParseInteger(string? text)
    {
        Match leading = Regex.Match(text ?? string.Empty, @"^\s*([-+]?[0-9]+)", RegexOptions.CultureInvariant);
        return leading.Success ? long.Parse(leading.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : 0;
    }

    private static ValueTask<bool> RunAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, long port, CancellationToken cancellationToken) =>
        steps.SetOptionsAndPerformAsync(
            easy,
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Proxy, context.Argument2),
                (CurlOption.Proxyport, port),
                (CurlOption.Proxyuserpwd, context.Arguments.Count > 4 ? context.Arguments[4] : null),
                (CurlOption.Proxyauth, (long)HttpAuthenticationMethods.Digest),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
            ],
            cancellationToken);
}
