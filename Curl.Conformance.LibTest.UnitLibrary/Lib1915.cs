using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options.Http;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1915.c</c>: two verbose transfers of the case's URL, where nothing listens, with HSTS enabled and a one-second
/// connect timeout. The first handle's read callback hands over <see cref="PreloadHosts"/> one a call and its write
/// callback writes each saved entry to stdout as <c>[index/total] [.]name expire</c> at cleanup, after which
/// <c>First request returned &lt;code&gt;</c> is written (a timeout counted as <c>CURLE_COULDNT_CONNECT</c>, as on Windows);
/// the second handle's read callback fails, and <c>Second request returned &lt;code&gt;</c> follows. The second perform's
/// code is the program's; a failing <c>easy_setopt</c> ends it with its code. The table is the one for a 64-bit
/// <c>time_t</c>, the <c>large-time</c> build Curl is.
/// </summary>
/// <param name="timeProvider">The clock behind the trace's time stamps.</param>
public sealed class Lib1915(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>The hosts and expiry times the first read callback hands over, in order; an empty time never expires.</summary>
    public static IReadOnlyList<(string Name, string Expire)> PreloadHosts { get; } =
    [
        ("1.example.com", "25250320 01:02:03"),
        ("2.example.com.", "25250320 03:02:01"),
        ("3.example.com", "25250319 01:02:03"),
        (".3.example.com", "22250319 01:02:03"),
        ("4.example.com", string.Empty),
        (".4.example.com", "20370319 01:02:03"),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1915";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, timeProvider, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="timeProvider">The clock behind the trace's time stamps.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
        if (!await TransferAsync(context, steps, library, trace, PreloadReader(context.StandardError), output, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        CurlCode first = steps.Result == CurlCode.OperationTimedOut ? CurlCode.CouldntConnect : steps.Result;
        output.Write($"First request returned {(int)first}\n");
        steps.Result = CurlCode.Ok;
        if (await TransferAsync(context, steps, library, trace, ReadFail, output, cancellationToken).ConfigureAwait(false))
        {
            output.Write($"Second request returned {(int)steps.Result}\n");
        }
    }

    /// <summary>The first read callback, <c>hstsread</c>: each of <see cref="PreloadHosts"/> in turn, then done.</summary>
    /// <param name="error">The program's stderr, where each host handed over is written as <c>add '&lt;host&gt;'</c>.</param>
    /// <returns>The callback, with its own position in the table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static HstsReadCallback PreloadReader(TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(error);
        int index = 0;
        return (out HstsEntry? entry) =>
        {
            if (index == PreloadHosts.Count)
            {
                entry = null;
                return HstsResult.Done;
            }

            var (name, expire) = PreloadHosts[index++];
            entry = new HstsEntry(name, IncludeSubDomains: false, expire);
            error.Write($"add '{name}'\n");
            return HstsResult.Ok;
        };
    }

    /// <summary>The second read callback, <c>hstsreadfail</c>: fails at once.</summary>
    /// <param name="entry">Never set to an entry.</param>
    /// <returns><see cref="HstsResult.Fail"/>.</returns>
    public static HstsResult ReadFail(out HstsEntry? entry)
    {
        entry = null;
        return HstsResult.Fail;
    }

    /// <summary>The write callback, <c>hstswrite</c>: writes the entry as <c>[index/total] [.]name expire</c>.</summary>
    /// <param name="output">The program's stdout.</param>
    /// <returns>The callback, which always answers <see cref="HstsResult.Ok"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static HstsWriteCallback Writer(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return (entry, index, total) =>
        {
            output.Write($"[{index}/{total}] {(entry.IncludeSubDomains ? "." : string.Empty)}{entry.Name} {entry.Expire ?? "unlimited"}\n");
            return HstsResult.Ok;
        };
    }

    private static async ValueTask<bool> TransferAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        LibTestDebugTrace trace,
        HstsReadCallback read,
        StreamWriter output,
        CancellationToken cancellationToken)
    {
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return false;
        }

        await using (easy.ConfigureAwait(false))
        {
            // Libcurl takes these values without fail, so their easy_setopt checks are left out.
            _ = steps.EasySetOptions(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Url, context.Url),
                        (CurlOption.Connecttimeout, 1L),
                        (CurlOption.Hstsreadfunction, read),
                        (CurlOption.Hstsreaddata, null),
                        (CurlOption.Hstswritefunction, Writer(output)),
                        (CurlOption.Hstswritedata, null),
                        (CurlOption.HstsCtrl, (long)HstsControl.Enable),
                        (CurlOption.Debugdata, trace),
                        (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                        (CurlOption.Verbose, 1L),
                    ]);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
