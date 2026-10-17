using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib520.c</c>, <c>lib523.c</c>, <c>lib524.c</c>, <c>lib542.c</c> and <c>lib549.c</c>: one verbose transfer
/// of the case's URL with the program's own options - <c>CURLOPT_FILETIME</c> for lib520; <c>CURLOPT_PROXY</c> from
/// <c>libtest_arg2</c>, <c>CURLOPT_PORT</c> 19999 and <c>CURLOPT_USERPWD</c> <c>xxx:yyy</c> for lib523;
/// <c>CURLOPT_UPLOAD</c> for lib524; <c>CURLOPT_NOBODY</c> without <c>CURLOPT_HEADER</c> for lib542; a proxy
/// with <c>CURLOPT_PROXY_TRANSFER_MODE</c>, and text mode when <c>libtest_arg3</c> is given, for lib549. The
/// program answers the transfer's code.
/// </summary>
public sealed class LibOneVerboseTransfer : ILibTest
{
    private readonly Func<LibTestContext, IReadOnlyList<(CurlOption Option, object? Value)>> programOptions;

    /// <summary>Creates the port named <paramref name="programName"/>, setting <paramref name="programOptions"/>.</summary>
    /// <param name="programName">The program's name, such as <c>lib520</c>.</param>
    /// <param name="programOptions">The options the program sets, <c>CURLOPT_URL</c> among them, after <c>CURLOPT_WRITEDATA</c> and before <c>CURLOPT_VERBOSE</c>.</param>
    private LibOneVerboseTransfer(string programName, Func<LibTestContext, IReadOnlyList<(CurlOption Option, object? Value)>> programOptions)
    {
        ProgramName = programName;
        this.programOptions = programOptions;
    }

    /// <summary>lib520: <c>CURLOPT_FILETIME</c> on.</summary>
    public static LibOneVerboseTransfer Filetime { get; } = new("lib520", context => [(CurlOption.Url, context.Url), (CurlOption.Filetime, 1L)]);

    /// <summary>lib523: through the proxy <c>libtest_arg2</c>, to port 19999, as <c>xxx:yyy</c>.</summary>
    public static LibOneVerboseTransfer ProxyPortUser { get; } = new(
        "lib523",
        context => [(CurlOption.Proxy, context.Argument2), (CurlOption.Url, context.Url), (CurlOption.Port, 19999L), (CurlOption.Userpwd, "xxx:yyy")]);

    /// <summary>lib524: <c>CURLOPT_UPLOAD</c> on.</summary>
    public static LibOneVerboseTransfer Upload { get; } = new("lib524", context => [(CurlOption.Url, context.Url), (CurlOption.Upload, 1L)]);

    /// <summary>lib542: <c>CURLOPT_NOBODY</c> on and <c>CURLOPT_HEADER</c> off.</summary>
    public static LibOneVerboseTransfer NobodyWithoutHeader { get; } = new(
        "lib542",
        context => [(CurlOption.Nobody, 1L), (CurlOption.Header, 0L), (CurlOption.Url, context.Url)]);

    /// <summary>
    /// lib549: through the proxy <c>libtest_arg2</c> with <c>CURLOPT_PROXY_TRANSFER_MODE</c> on, and
    /// <c>CURLOPT_TRANSFERTEXT</c> on when <c>libtest_arg3</c> is given.
    /// </summary>
    public static LibOneVerboseTransfer ProxyTransferMode { get; } = new("lib549", ProxyTransferModeOptions);

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
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
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
            options.AddRange(programOptions(context));
            options.Add((CurlOption.Verbose, 1L));
            await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<(CurlOption Option, object? Value)> ProxyTransferModeOptions(LibTestContext context)
    {
        List<(CurlOption Option, object? Value)> options =
            [(CurlOption.Proxy, context.Argument2), (CurlOption.Url, context.Url), (CurlOption.ProxyTransferMode, 1L)];
        if (context.Argument3 is not null)
        {
            options.Add((CurlOption.Transfertext, 1L));
        }

        return options;
    }
}
