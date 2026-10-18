using System.Globalization;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib590.c</c>: a download with headers on through the proxy in <c>libtest_arg2</c>, allowing Basic, Digest and
/// NTLM there (not Negotiate) as <c>me:password</c>, set both by name and password and as one string. Once it
/// succeeded, a <c>CURLINFO_PROXYAUTH_USED</c> other than NTLM prints
/// <c>CURLINFO_PROXYAUTH_USED did not say NTLM</c> on stdout.
/// </summary>
public sealed class Lib590 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib590";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: <c>curl_easy_init</c>, the options, the perform and the check.</summary>
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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            if (await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Header, 1L),
                    (CurlOption.Proxyauth, (long)(HttpAuthenticationMethods.Basic | HttpAuthenticationMethods.Digest | HttpAuthenticationMethods.Ntlm)),
                    (CurlOption.Proxy, context.Argument2),
                    (CurlOption.Proxyusername, "me"),
                    (CurlOption.Proxypassword, "password"),
                    (CurlOption.Proxyuserpwd, "me:password"),
                ],
                cancellationToken).ConfigureAwait(false))
            {
                CheckProxyAuthUsed(context, steps, easy);
            }
        }
    }

    /// <summary>Reads <c>CURLINFO_PROXYAUTH_USED</c> into the program's result and says so on stdout when it is not NTLM.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps.</param>
    /// <param name="easy">The handle, its transfer done.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void CheckProxyAuthUsed(LibTestContext context, LibTestSteps steps, CurlEasy easy)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _ = steps.EasyGetInfo(easy, CurlInfo.ProxyauthUsed, out object? used);
        if (Convert.ToInt64(used, CultureInfo.InvariantCulture) != (long)HttpAuthenticationMethods.Ntlm)
        {
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write("CURLINFO_PROXYAUTH_USED did not say NTLM\n");
        }
    }
}
