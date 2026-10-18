using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1955.c</c> to <c>lib1959.c</c>: a verbose request of the case's URL signed with <c>CURLOPT_AWS_SIGV4</c>
/// <c>xxx</c> and the credentials <c>xxx</c>, without headers in the output, connected through
/// <c>CURLOPT_CONNECT_TO</c> <c>libtest_arg2</c> (no list when it is not given) and sending each program's own header
/// list, which the signature must cover as upstream's does. The transfer's code is the program's. A failing
/// <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// The <c>curl_slist_append</c> checks are left out: Libcurl's list append does not fail.
/// </summary>
public sealed class LibAwsSigV4Headers : ILibTest
{
    /// <summary>The provider string and the credentials every program sets, <c>xxx</c>.</summary>
    public const string ProviderAndCredentials = "xxx";

    /// <summary>The <c>Content-Type</c> header every program sends.</summary>
    public const string JsonContentType = "Content-Type: application/json";

    /// <summary>The SHA-256 of no bytes, the content hash lib1956 sends and lib1959 sends twice over.</summary>
    public const string EmptyContentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private LibAwsSigV4Headers(string programName, IReadOnlyList<string> headers)
    {
        ProgramName = programName;
        Headers = headers;
    }

    /// <summary><c>lib1955</c>: headers with no value, an empty one, an invalid one, inner whitespace and mixed case.</summary>
    public static LibAwsSigV4Headers Lib1955 { get; } = new(
        "lib1955",
        ["test3: 1234", JsonContentType, "test2;", "test1:", "test0", "test_space: t\ts  m\t   end    ", "tesMixCase: MixCase"]);

    /// <summary><c>lib1956</c>: the content hash header with the hash of no bytes.</summary>
    public static LibAwsSigV4Headers Lib1956 { get; } = new("lib1956", [JsonContentType, "X-Xxx-Content-Sha256: " + EmptyContentSha256]);

    /// <summary><c>lib1957</c>: the content hash header with an arbitrary value.</summary>
    public static LibAwsSigV4Headers Lib1957 { get; } = new("lib1957", [JsonContentType, "X-Xxx-Content-Sha256: arbitrary"]);

    /// <summary><c>lib1958</c>: the content hash header with an arbitrary value between whitespace.</summary>
    public static LibAwsSigV4Headers Lib1958 { get; } = new("lib1958", [JsonContentType, "X-Xxx-Content-Sha256: \tarbitrary "]);

    /// <summary><c>lib1959</c>: the content hash header with a value too long to be a hash.</summary>
    public static LibAwsSigV4Headers Lib1959 { get; } = new(
        "lib1959",
        [JsonContentType, "X-Xxx-Content-Sha256: " + EmptyContentSha256 + EmptyContentSha256]);

    /// <inheritdoc/>
    public string ProgramName { get; }

    /// <summary>The program's <c>CURLOPT_HTTPHEADER</c> list, in its order.</summary>
    public IReadOnlyList<string> Headers { get; }

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
    public async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
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

            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.AwsSigv4, ProviderAndCredentials),
                    (CurlOption.Userpwd, ProviderAndCredentials),
                    (CurlOption.Header, 0L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.ConnectTo, context.Argument2 is null ? null : CurlSlist.Append(null, context.Argument2)),
                    (CurlOption.Httpheader, headers),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
