using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1525.c</c>, <c>lib1526.c</c> and <c>lib1527.c</c>: a verbose upload of <see cref="Data"/> (read by
/// <see cref="HelloCloudReader"/>) with headers in the output, tunnelled through the HTTP proxy <c>libtest_arg2</c>
/// with <c>CURLOPT_HTTPPROXYTUNNEL</c>, each program giving its own server and proxy header lists and
/// <c>CURLOPT_HEADEROPT</c>; the transfer's code is the program's. A failing <c>curl_global_init</c> or
/// <c>curl_easy_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>. The
/// <c>curl_slist_append</c> checks are left out: Libcurl's list append does not fail.
/// </summary>
public sealed class LibProxyTunnelUpload : ILibTest
{
    /// <summary><c>CURLHEADER_UNIFIED</c>: the server headers go to the proxy too.</summary>
    public const long HeaderUnified = 0;

    /// <summary><c>CURLHEADER_SEPARATE</c>: the proxy gets only the proxy headers.</summary>
    public const long HeaderSeparate = 1;

    /// <summary><c>CURLPROXY_HTTP</c>.</summary>
    public const long ProxyHttp = 0;

    private readonly Func<IReadOnlyList<(CurlOption Option, object? Value)>> headerOptions;

    private LibProxyTunnelUpload(string programName, Func<IReadOnlyList<(CurlOption Option, object? Value)>> headerOptions)
    {
        ProgramName = programName;
        this.headerOptions = headerOptions;
    }

    /// <summary><c>t1525_data</c> and its siblings: the uploaded body.</summary>
    public static ReadOnlyMemory<byte> Data { get; } = "Hello Cloud!\n"u8.ToArray();

    /// <summary><c>lib1525</c>: one list, <c>User-Agent: Http Agent</c>, for server and proxy, unified.</summary>
    public static LibProxyTunnelUpload Unified { get; } = new("lib1525", UnifiedHeaderOptions);

    /// <summary>
    /// <c>lib1526</c>: <c>User-Agent: Http Agent</c> for the server, <c>User-Agent: Proxy Agent</c> and
    /// <c>Expect:</c> for the proxy, separate.
    /// </summary>
    public static LibProxyTunnelUpload Separate { get; } = new("lib1526", SeparateHeaderOptions);

    /// <summary>
    /// <c>lib1527</c>: <c>User-Agent: Http Agent</c> and <c>Expect: 100-continue</c> for the server, no proxy list,
    /// unified (set last).
    /// </summary>
    public static LibProxyTunnelUpload ExpectContinue { get; } = new("lib1527", ExpectContinueHeaderOptions);

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
            List<(CurlOption Option, object? Value)> options =
                [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url), (CurlOption.Proxy, context.Argument2)];
            options.AddRange(headerOptions());
            options.AddRange(
            [
                (CurlOption.Post, 0L),
                (CurlOption.Upload, 1L),
                (CurlOption.Verbose, 1L),
                (CurlOption.Proxytype, ProxyHttp),
                (CurlOption.Header, 1L),
                (CurlOption.Readfunction, (ReadCallback)HelloCloudReader.Read),
                (CurlOption.Httpproxytunnel, 1L),
                (CurlOption.Infilesize, (long)Data.Length),
            ]);
            _ = await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<(CurlOption Option, object? Value)> UnifiedHeaderOptions()
    {
        CurlSlist headers = CurlSlist.Append(null, "User-Agent: Http Agent");
        return [(CurlOption.Httpheader, headers), (CurlOption.Proxyheader, headers), (CurlOption.Headeropt, HeaderUnified)];
    }

    private static List<(CurlOption Option, object? Value)> SeparateHeaderOptions()
    {
        CurlSlist proxyHeaders = CurlSlist.Append(CurlSlist.Append(null, "User-Agent: Proxy Agent"), "Expect:");
        return
        [
            (CurlOption.Httpheader, CurlSlist.Append(null, "User-Agent: Http Agent")),
            (CurlOption.Proxyheader, proxyHeaders),
            (CurlOption.Headeropt, HeaderSeparate),
        ];
    }

    private static List<(CurlOption Option, object? Value)> ExpectContinueHeaderOptions() =>
    [
        (CurlOption.Httpheader, CurlSlist.Append(CurlSlist.Append(null, "User-Agent: Http Agent"), "Expect: 100-continue")),
        (CurlOption.Headeropt, HeaderUnified),
    ];

    /// <summary><c>t1525_read_cb</c> and its siblings.</summary>
    public static class HelloCloudReader
    {
        /// <summary>
        /// Copies <see cref="Data"/> whole into a buffer that holds it, and answers its length each call, so the upload
        /// ends only at <c>CURLOPT_INFILESIZE</c>; a smaller buffer gets nothing yet the same length, as upstream's does.
        /// </summary>
        /// <param name="buffer">Where to copy it.</param>
        /// <returns><see cref="Data"/>'s length.</returns>
        public static long Read(Span<byte> buffer)
        {
            if (buffer.Length >= Data.Length)
            {
                Data.Span.CopyTo(buffer);
            }

            return Data.Length;
        }
    }
}
