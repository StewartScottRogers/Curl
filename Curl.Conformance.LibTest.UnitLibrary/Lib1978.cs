using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1978.c</c>: an empty verbose upload of the case's URL signed with <c>CURLOPT_AWS_SIGV4</c>
/// <c>aws:amz:us-east-1:s3</c>, sending <see cref="Headers"/> (duplicates, differently cased names, names with no colon
/// and with no value, which the signature must merge) and connected through <c>CURLOPT_CONNECT_TO</c> <c>libtest_arg2</c>
/// (no list when it is not given); when it succeeds, the same upload again unsigned. The last transfer's code is the
/// program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>; the <c>curl_slist_append</c> check is left out: Libcurl's list append does
/// not fail.
/// </summary>
public sealed class Lib1978 : ILibTest
{
    /// <summary>The headers the program sends, in its order.</summary>
    public static IReadOnlyList<string> Headers { get; } =
    [
        "x-amz-meta-test: test2",
        "some-other-header: value",
        "x-amz-meta-test: test1",
        "duplicate-header: duplicate",
        "header-no-value",
        "x-amz-meta-test: test3",
        "X-amz-meta-test2: test2",
        "x-amz-meta-blah: blah",
        "x-Amz-meta-test2: test1",
        "x-amz-Meta-test2: test3",
        "curr-header-no-colon",
        "curr-header-no-colon: value",
        "next-header-no-colon: value",
        "next-header-no-colon",
        "duplicate-header: duplicate",
        "header-no-value;",
        "header-no-value;",
        "header-some-no-value;",
        "header-some-no-value: value",
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1978";

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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            CurlSlist? headers = null;
            foreach (string header in Headers)
            {
                headers = CurlSlist.Append(headers, header);
            }

            if (await steps.SetOptionsAndPerformAsync(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Upload, 1L),
                        (CurlOption.Infilesize, 0L),
                        (CurlOption.Verbose, 1L),
                        (CurlOption.AwsSigv4, "aws:amz:us-east-1:s3"),
                        (CurlOption.Header, 0L),
                        (CurlOption.Url, context.Url),
                        (CurlOption.Httpheader, headers),
                        (CurlOption.ConnectTo, context.Argument2 is null ? null : CurlSlist.Append(null, context.Argument2)),
                    ],
                    cancellationToken).ConfigureAwait(false))
            {
                _ = await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.AwsSigv4, null)], cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
