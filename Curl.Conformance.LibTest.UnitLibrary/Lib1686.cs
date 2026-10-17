using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1686.c</c>: <c>argv[1]</c> is the server's address and <c>libtest_arg2</c> its port. <see cref="FirstHost"/> and
/// <see cref="SecondHost"/> resolve to it through <c>CURLOPT_RESOLVE</c>, and one handle with Digest authentication as
/// <c>alice:bond</c> fetches <c>/api</c> from the first host, then <c>/hook</c> from the second, then, as
/// <c>bob:secret</c>, <c>/hook</c> again, the body thrown away; the first failing perform ends the program with its code.
/// </summary>
public sealed class Lib1686 : ILibTest
{
    /// <summary><c>FIRSTHOST</c>.</summary>
    public const string FirstHost = "first.test";

    /// <summary><c>SECONDHOST</c>.</summary>
    public const string SecondHost = "second.test";

    /// <inheritdoc/>
    public string ProgramName => "lib1686";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
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
        ArgumentNullException.ThrowIfNull(library);
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            string? port = context.Argument2;
            string firstUrl = $"http://{FirstHost}:{port}/api";
            string secondUrl = $"http://{SecondHost}:{port}/hook";
            CurlSlist hosts = CurlSlist.Append(CurlSlist.Append(null, $"{FirstHost}:{port}:{context.Url}"), $"{SecondHost}:{port}:{context.Url}");
            _ = steps.EasySetOptions(
                easy,
                [
                    (CurlOption.Resolve, hosts),
                    (CurlOption.Httpauth, (long)HttpAuthenticationMethods.Digest),
                    (CurlOption.Userpwd, "alice:bond"),
                    (CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite),
                ]);
            _ = await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Url, firstUrl)], cancellationToken).ConfigureAwait(false)
                && await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Url, secondUrl)], cancellationToken).ConfigureAwait(false)
                && await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Userpwd, "bob:secret"), (CurlOption.Url, secondUrl)], cancellationToken).ConfigureAwait(false);
        }
    }
}
