using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1933.c</c> to <c>lib1938.c</c>: a verbose request of the case's URL signed with <c>CURLOPT_AWS_SIGV4</c>, without
/// headers in the output, connected through <c>CURLOPT_CONNECT_TO</c> <c>libtest_arg2</c> (no list when it is not
/// given) and sending <c>Content-Type: application/json</c>; each program gives its own provider string and the options
/// around it (credentials, a <c>POST</c> body). The transfer's code is the program's. A failing <c>curl_global_init</c> or
/// <c>curl_easy_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>. The
/// <c>curl_slist_append</c> checks are left out: Libcurl's list append does not fail.
/// </summary>
public sealed class LibAwsSigV4Request : ILibTest
{
    /// <summary>The credentials lib1934 to lib1936 set, <c>xxx:yyy</c>.</summary>
    public const string ShortCredentials = "xxx:yyy";

    /// <summary>The provider string lib1937 and lib1938 sign with.</summary>
    public const string ProviderRegionService = "provider1:provider2:region:service";

    /// <summary>The credentials lib1937 and lib1938 set, <c>keyId:SecretKey</c>.</summary>
    public const string KeyCredentials = "keyId:SecretKey";

    private readonly Func<IReadOnlyList<(CurlOption Option, object? Value)>> signingOptions;

    private readonly Func<IReadOnlyList<(CurlOption Option, object? Value)>> bodyOptions;

    private LibAwsSigV4Request(
        string programName,
        Func<IReadOnlyList<(CurlOption Option, object? Value)>> signingOptions,
        Func<IReadOnlyList<(CurlOption Option, object? Value)>> bodyOptions)
    {
        ProgramName = programName;
        this.signingOptions = signingOptions;
        this.bodyOptions = bodyOptions;
    }

    /// <summary>lib1938's body, <c>post\0Data</c>, sent with its size.</summary>
    public static ReadOnlyMemory<byte> BinaryPostData { get; } = "post\0Data"u8.ToArray();

    /// <summary><c>lib1933</c>: the provider string <c>xxx</c> and no credentials (the URL carries them).</summary>
    public static LibAwsSigV4Request Lib1933 { get; } = new("lib1933", () => [(CurlOption.AwsSigv4, "xxx")], NoBody);

    /// <summary><c>lib1934</c>: the provider string <c>xxx:yyy</c> with <see cref="ShortCredentials"/>.</summary>
    public static LibAwsSigV4Request Lib1934 { get; } = new("lib1934", () => Short("xxx:yyy"), NoBody);

    /// <summary><c>lib1935</c>: the provider string <c>xxx:yyy:rrr</c> with <see cref="ShortCredentials"/>.</summary>
    public static LibAwsSigV4Request Lib1935 { get; } = new("lib1935", () => Short("xxx:yyy:rrr"), NoBody);

    /// <summary><c>lib1936</c>: the provider string <c>xxx:yyy:rrr:sss</c> with <see cref="ShortCredentials"/>.</summary>
    public static LibAwsSigV4Request Lib1936 { get; } = new("lib1936", () => Short("xxx:yyy:rrr:sss"), NoBody);

    /// <summary><c>lib1937</c>: a <c>POST</c> of <c>postData</c> signed with <see cref="ProviderRegionService"/>.</summary>
    public static LibAwsSigV4Request Lib1937 { get; } = new("lib1937", Post, () => [(CurlOption.Postfields, "postData")]);

    /// <summary><c>lib1938</c>: a <c>POST</c> of <see cref="BinaryPostData"/>, holding a NUL, with its size.</summary>
    public static LibAwsSigV4Request Lib1938 { get; } = new(
        "lib1938",
        Post,
        () => [(CurlOption.Postfields, BinaryPostData.ToArray()), (CurlOption.Postfieldsize, (long)BinaryPostData.Length)]);

    /// <inheritdoc/>
    public string ProgramName { get; }

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
            List<(CurlOption Option, object? Value)> options = [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Verbose, 1L)];
            options.AddRange(signingOptions());
            options.AddRange(
            [
                (CurlOption.Header, 0L),
                (CurlOption.Url, context.Url),
                (CurlOption.ConnectTo, context.Argument2 is null ? null : CurlSlist.Append(null, context.Argument2)),
                (CurlOption.Httpheader, CurlSlist.Append(null, "Content-Type: application/json")),
            ]);
            options.AddRange(bodyOptions());
            _ = await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<(CurlOption Option, object? Value)> Short(string provider) =>
        [(CurlOption.AwsSigv4, provider), (CurlOption.Userpwd, ShortCredentials)];

    private static List<(CurlOption Option, object? Value)> Post() =>
        [(CurlOption.Post, 1L), (CurlOption.AwsSigv4, ProviderRegionService), (CurlOption.Userpwd, KeyCredentials)];

    private static List<(CurlOption Option, object? Value)> NoBody() => [];
}
