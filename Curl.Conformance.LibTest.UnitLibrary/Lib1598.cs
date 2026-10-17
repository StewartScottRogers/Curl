using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1598.c</c>: a verbose chunked POST of <see cref="PostData"/> to the case's URL with the <c>Trailer:</c> header
/// (<see cref="Lib1591.TrailerHeader"/>) and <c>Transfer-Encoding: chunked</c>, the trailers added by
/// <see cref="Lib1591.AddTrailers"/>, which this program repeats. The perform's code is the program's.
/// </summary>
public sealed class Lib1598 : ILibTest
{
    /// <summary>The posted body, <c>post_data</c>.</summary>
    public const string PostData = "xxx=yyy&aaa=bbbbb";

    /// <inheritdoc/>
    public string ProgramName => "lib1598";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
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
                    (CurlOption.Httpheader, CurlSlist.Append(CurlSlist.Append(null, Lib1591.TrailerHeader), "Transfer-Encoding: chunked")),
                    (CurlOption.Postfieldsize, (long)PostData.Length),
                    (CurlOption.Postfields, PostData),
                    (CurlOption.Trailerfunction, new TrailerCallback(Lib1591.AddTrailers)),
                    (CurlOption.Trailerdata, null),
                    (CurlOption.Verbose, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
