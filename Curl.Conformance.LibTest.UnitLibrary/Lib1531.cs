using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1531.c</c>: a POST of the binary <see cref="PostData"/>, its options set after the handle joins a multi
/// handle, driven by unchecked <c>curl_multi_perform</c> calls with a <c>curl_multi_fdset</c> and a wait of at most a
/// second (<c>curl_multi_timeout</c>'s, under two seconds) or 100 ms with no sockets between them, then
/// <c>HTTP transfer completed with status N</c> on stdout for its <c>CURLMSG_DONE</c> message. A failing
/// <c>curl_multi_fdset</c> writes <c>curl_multi_fdset() failed, code N.</c> on stderr and stops the loop; the hang
/// timeout ends the program. The handles are not checked, as upstream's are not, but a missing one ends the program
/// as <c>easy_init</c> and <c>multi_init</c> would; the message loop reads one message, the only type being done.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib1531(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>testdata</c>: the posted bytes, a NUL among them.</summary>
    public static readonly byte[] PostData = ".abc\0xyz"u8.ToArray();

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1531";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            if (!steps.MultiInit(library, out CurlMulti? multi))
            {
                return;
            }

            await using (multi.ConfigureAwait(false))
            {
                _ = multi.AddHandle(easy);
                _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
                _ = easy.SetOption(CurlOption.Url, context.Url);
                _ = easy.SetOption(CurlOption.PostfieldsizeLarge, (long)PostData.Length);
                _ = easy.SetOption(CurlOption.Postfields, PostData);
                if (await LoopAsync(context, steps, multi, clock, cancellationToken).ConfigureAwait(false)
                    && multi.InfoRead(out _) is { } message)
                {
                    using StreamWriter output = LibTestRun.StandardOutputWriter(context);
                    output.Write($"HTTP transfer completed with status {(int)message.Result}\n");
                }
            }
        }
    }

    /// <summary>The wait between two performs: <c>curl_multi_timeout</c>'s, a second when it has none or it is two seconds or more.</summary>
    /// <param name="multiTimeoutMilliseconds">What <c>curl_multi_timeout</c> gave, -1 for none.</param>
    /// <returns>The wait in milliseconds.</returns>
    public static int SelectTimeoutMilliseconds(long multiTimeoutMilliseconds) =>
        multiTimeoutMilliseconds is >= 0 and < 2000 ? (int)multiTimeoutMilliseconds : 1000;

    // The perform loop; false when the hang timeout ended the program.
    private static async ValueTask<bool> LoopAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, LibTestClock clock, CancellationToken cancellationToken)
    {
        var (_, running) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
        if (!steps.AbortOnTestTimeout(clock))
        {
            return false;
        }

        while (running > 0)
        {
            var (_, multiTimeout) = multi.Timeout();
            CurlMultiCode code = multi.FdSet(null, null, null, out nint maxSocket);
            if (code != CurlMultiCode.Ok)
            {
                context.StandardError.Write($"curl_multi_fdset() failed, code {(int)code}.\n");
                return true;
            }

            await WaitAsync(multi, clock, maxSocket, SelectTimeoutMilliseconds(multiTimeout), cancellationToken).ConfigureAwait(false);
            (_, running) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (!steps.AbortOnTestTimeout(clock))
            {
                return false;
            }
        }

        return true;
    }

    // select() on the handle's sockets, or curlx_wait_ms(100) when it has none.
    private static async ValueTask WaitAsync(CurlMulti multi, LibTestClock clock, nint maxSocket, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (maxSocket == -1)
        {
            await clock.WaitAsync(100, cancellationToken).ConfigureAwait(false);
            return;
        }

        _ = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
    }
}
