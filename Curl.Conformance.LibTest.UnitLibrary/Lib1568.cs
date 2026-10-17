using System.Globalization;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1568.c</c>: one verbose GET with headers, Digest credentials <c>testuser:testpass</c>, the user agent
/// <c>lib1568</c>, at most 50 redirects and <c>CURLOPT_PORT</c> from <c>libtest_arg2</c>, answering the perform's code.
/// An <c>libtest_arg2</c> that is not a number from 0 to 65535 (<c>curlx_str_number</c>) ends with
/// <see cref="LibTestErrorCodes.MajorBad"/> before <c>curl_global_init</c>, as does a failing init; the easy handle is
/// not checked, as upstream does not check it.
/// </summary>
public sealed class Lib1568 : ILibTest
{
    /// <summary>The largest port <c>curlx_str_number</c> takes: <c>0xffff</c>.</summary>
    public const long MaximumPort = 0xffff;

    /// <inheritdoc/>
    public string ProgramName => "lib1568";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryParsePort(context.Argument2, out long port))
        {
            return Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad));
        }

        return RunWithPortAsync(context, port, cancellationToken);
    }

    /// <summary>
    /// <c>curlx_str_number(&amp;libtest_arg2, &amp;port, 0xffff)</c>: a run of decimal digits at the start of
    /// <paramref name="text"/> no larger than <see cref="MaximumPort"/>.
    /// </summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <param name="port">The number, or 0 when there is none.</param>
    /// <returns>Whether a number was read.</returns>
    public static bool TryParsePort(string? text, out long port)
    {
        port = 0;
        string digits = string.Concat((text ?? string.Empty).TakeWhile(char.IsAsciiDigit));
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out port)
            && port <= MaximumPort;
    }

    private static async Task<LibTestResult> RunWithPortAsync(LibTestContext context, long port, CancellationToken cancellationToken)
    {
        var steps = new LibTestSteps(context.StandardError);
        if (context.Engine is null)
        {
            return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
        }

        CurlLibrary library = CurlGlobal.Init(context.Engine, CurlGlobalFlags.All);
        await using (library.ConfigureAwait(false))
        {
            CurlEasy easy = library.EasyInit();
            await using (easy.ConfigureAwait(false))
            {
                _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
                _ = easy.SetOption(CurlOption.Url, context.Url);
                _ = easy.SetOption(CurlOption.Verbose, 1L);
                _ = easy.SetOption(CurlOption.Header, 1L);
                _ = easy.SetOption(CurlOption.Userpwd, "testuser:testpass");
                _ = easy.SetOption(CurlOption.Useragent, "lib1568");
                _ = easy.SetOption(CurlOption.Httpauth, (long)HttpAuthenticationMethods.Digest);
                _ = easy.SetOption(CurlOption.Maxredirs, 50L);
                _ = easy.SetOption(CurlOption.Port, port);
                _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            }
        }

        return steps.ToLibTestResult();
    }
}
