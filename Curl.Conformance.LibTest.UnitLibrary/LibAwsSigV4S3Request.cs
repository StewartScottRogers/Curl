using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1970.c</c>, <c>lib1971.c</c>, <c>lib1973.c</c>, <c>lib1974.c</c> and <c>lib1975.c</c>: a verbose request of the
/// case's URL signed with <c>CURLOPT_AWS_SIGV4</c> <see cref="Provider"/> and the credentials <c>xxx</c>, without headers
/// in the output, sending each program's header list (none for lib1974) and connected through <c>CURLOPT_CONNECT_TO</c>
/// <c>libtest_arg2</c> (no list when it is not given); each program sets its own body options first (an upload of
/// nothing, a <c>POST</c>, none), which decide the content hash the signature carries. The transfer's code is the
/// program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>. The <c>curl_slist_append</c> checks are left out: Libcurl's list append
/// does not fail. lib1972, the same request with a MIME body, is <see cref="Lib1972"/>.
/// </summary>
public sealed class LibAwsSigV4S3Request : ILibTest
{
    /// <summary>The provider string every program signs with, <c>aws:amz:us-east-1:s3</c>.</summary>
    public const string Provider = "aws:amz:us-east-1:s3";

    /// <summary>The credentials every program sets, <c>xxx</c>.</summary>
    public const string Credentials = "xxx";

    /// <summary>The <c>Content-Type</c> header every program but lib1974 sends.</summary>
    public const string JsonContentType = "Content-Type: application/json";

    /// <summary>The content hash header lib1975 sends, with the SHA-256 of no bytes.</summary>
    public const string EmptyContentSha256Header =
        "X-Amz-Content-Sha256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>lib1973's body, <c>post fields</c> and a newline.</summary>
    public const string PostFields = "post fields\n";

    private readonly Func<IReadOnlyList<(CurlOption Option, object? Value)>> bodyOptions;

    private LibAwsSigV4S3Request(string programName, Func<IReadOnlyList<(CurlOption Option, object? Value)>> bodyOptions, IReadOnlyList<string> headers)
    {
        ProgramName = programName;
        this.bodyOptions = bodyOptions;
        Headers = headers;
    }

    /// <summary><c>lib1970</c>: an upload with <c>CURLOPT_INFILESIZE</c> 0.</summary>
    public static LibAwsSigV4S3Request Lib1970 { get; } = new(
        "lib1970",
        () => [(CurlOption.Upload, 1L), (CurlOption.Infilesize, 0L)],
        [JsonContentType]);

    /// <summary><c>lib1971</c>: an upload of unknown size whose read callback ends it at once.</summary>
    public static LibAwsSigV4S3Request Lib1971 { get; } = new("lib1971", EmptyUpload, [JsonContentType]);

    /// <summary><c>lib1973</c>: a <c>POST</c> of <see cref="PostFields"/>.</summary>
    public static LibAwsSigV4S3Request Lib1973 { get; } = new("lib1973", () => [(CurlOption.Postfields, PostFields)], [JsonContentType]);

    /// <summary><c>lib1974</c>: a plain request with no header list.</summary>
    public static LibAwsSigV4S3Request Lib1974 { get; } = new("lib1974", () => [], []);

    /// <summary><c>lib1975</c>: lib1971's upload with the content hash header of no bytes.</summary>
    public static LibAwsSigV4S3Request Lib1975 { get; } = new("lib1975", EmptyUpload, [JsonContentType, EmptyContentSha256Header]);

    /// <inheritdoc/>
    public string ProgramName { get; }

    /// <summary>The program's <c>CURLOPT_HTTPHEADER</c> list, in its order; empty when it sets none.</summary>
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
            List<(CurlOption Option, object? Value)> options = [(CurlOption.Writedata, context.StandardOutput)];
            options.AddRange(bodyOptions());
            options.AddRange(SignedRequestOptions(context, Headers));
            _ = await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The options every program sets after its body options: verbose, the signature, the credentials, no headers in
    /// the output, the URL, the header list (left unset when <paramref name="headers"/> is empty) and the connect-to list.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="headers">The program's header list.</param>
    /// <returns>The options in the programs' order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static List<(CurlOption Option, object? Value)> SignedRequestOptions(LibTestContext context, IReadOnlyList<string> headers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(headers);
        List<(CurlOption Option, object? Value)> options =
        [
            (CurlOption.Verbose, 1L),
            (CurlOption.AwsSigv4, Provider),
            (CurlOption.Userpwd, Credentials),
            (CurlOption.Header, 0L),
            (CurlOption.Url, context.Url),
        ];
        CurlSlist? list = null;
        foreach (string header in headers)
        {
            list = CurlSlist.Append(list, header);
        }

        if (list is not null)
        {
            options.Add((CurlOption.Httpheader, list));
        }

        options.Add((CurlOption.ConnectTo, context.Argument2 is null ? null : CurlSlist.Append(null, context.Argument2)));
        return options;
    }

    /// <summary><c>t1971_read_cb</c> and <c>t1975_read_cb</c>: no bytes, ending the upload.</summary>
    /// <param name="buffer">The buffer, left untouched.</param>
    /// <returns>0.</returns>
    public static long ReadNothing(Span<byte> buffer) => 0;

    private static List<(CurlOption Option, object? Value)> EmptyUpload() =>
        [(CurlOption.Upload, 1L), (CurlOption.Readfunction, (ReadCallback)ReadNothing)];
}
