using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1919.c</c>: a handle with <c>CURLAUTH_BEARER</c>, <c>CURLOPT_XOAUTH2_BEARER</c> <see cref="Bearer"/> and
/// <c>CURLOPT_SASL_AUTHZID</c> <see cref="AuthorizationIdentity"/> fetches the case's URL twice, the second time over the
/// reused connection. The first failing perform ends the program with its code, as does a failing <c>easy_setopt</c>.
/// </summary>
public sealed class Lib1919 : ILibTest
{
    /// <summary>The bearer token.</summary>
    public const string Bearer = "c4e448d652a961fda0ab64f882c8c161d5985f805d45d80c9ddca1";

    /// <summary>The SASL authorization identity.</summary>
    public const string AuthorizationIdentity = "c4e448d652a961fda0ab64f882c8c161d5985f805d45d80c9ddca2";

    /// <inheritdoc/>
    public string ProgramName => "lib1919";

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
            // Libcurl takes these values without fail, so their easy_setopt checks are left out.
            _ = steps.EasySetOptions(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Httpauth, (long)HttpAuthenticationMethods.Bearer),
                    (CurlOption.Xoauth2Bearer, Bearer),
                    (CurlOption.SaslAuthzid, AuthorizationIdentity),
                    (CurlOption.Url, context.Url),
                ]);
            if (await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
