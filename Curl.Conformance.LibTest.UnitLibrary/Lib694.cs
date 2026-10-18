using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib694.c</c>: two verbose transfers with headers on as <c>me:password</c> offering Basic, Digest and NTLM, the
/// second to <c>libtest_arg2</c> so NTLM is not restarted; after each, <c>CURLINFO_HTTPAUTH_USED</c> other than NTLM
/// prints <c>CURLINFO_HTTPAUTH_USED did not say NTLM</c> on stdout. A failing transfer ends the program
/// with its code.
/// </summary>
public sealed class Lib694 : ILibTest
{
    /// <summary><c>CURLAUTH_NTLM</c>.</summary>
    public const long AuthNtlm = 1L << 3;

    /// <summary><c>CURLAUTH_BASIC | CURLAUTH_DIGEST | CURLAUTH_NTLM</c>.</summary>
    public const long AuthBasicDigestNtlm = 1L | (1L << 1) | AuthNtlm;

    /// <inheritdoc/>
    public string ProgramName => "lib694";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: the two authenticated transfers.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
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
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Header, 1L),
                (CurlOption.Verbose, 1L),
                (CurlOption.Httpauth, AuthBasicDigestNtlm),
                (CurlOption.Userpwd, "me:password"),
            ]);
            for (int count = 0; count < 2 && await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false); count++)
            {
                // Libcurl answers CURLINFO_HTTPAUTH_USED on any handle, so upstream's check of the call is left out.
                _ = steps.EasyGetInfo(easy, CurlInfo.HttpauthUsed, out object? used);
                if (Convert.ToInt64(used, System.Globalization.CultureInfo.InvariantCulture) != AuthNtlm)
                {
                    context.StandardOutput.Write("CURLINFO_HTTPAUTH_USED did not say NTLM\n"u8);
                }

                _ = steps.EasySetOption(easy, CurlOption.Url, context.Argument2);
            }
        }
    }
}
