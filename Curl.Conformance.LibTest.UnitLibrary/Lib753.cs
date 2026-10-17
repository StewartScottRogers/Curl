using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib753.c</c>: a verbose FTP transfer <c>EASY1</c> on a multi handle pauses its body; once its header callback
/// has seen <c>230 Welcome you silly person</c>, the program cleans <c>EASY1</c> up without removing it from the multi
/// handle and adds <c>EASY2</c>, which does not pause, for the same URL, so the connection <c>EASY1</c> held must not be
/// reused. Each callback and <c>curl_multi_perform</c> writes its line on stderr, with a time-stamped trace.
/// </summary>
/// <param name="timeProvider">The clock behind <c>start_test_timing</c> and the trace's time stamps.</param>
public sealed class Lib753(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib753";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: the multi handle, <c>EASY1</c>, the loop, then the cleanup.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        var run = new Run(context, steps, library, new LibTestClock(timeProvider), new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true));
        await run.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>struct t753_transfer_status</c>: one handle's name, whether it pauses, and what it has seen.</summary>
    /// <param name="name">The handle's name in each line, <c>EASY1</c> or <c>EASY2</c>.</param>
    /// <param name="error">The program's stderr.</param>
    public sealed class TransferStatus(string name, TextWriter error)
    {
        /// <summary>Whether the write callback pauses the body.</summary>
        public bool Pause { get; set; } = true;

        /// <summary>Whether the last write callback paused.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>Whether the header callback has seen the FTP welcome.</summary>
        public bool SeenWelcome { get; private set; }

        /// <summary><c>t753_write_cb</c>: pauses or consumes the body, writing which.</summary>
        /// <param name="data">The body bytes.</param>
        /// <returns><see cref="WriteCallbackResult.Pause"/>, or the byte count.</returns>
        public long Write(ReadOnlySpan<byte> data)
        {
            IsPaused = Pause;
            error.Write($"[{name}] write_cb(len={data.Length}), {(Pause ? "PAUSE" : "CONSUME")}\n");
            return Pause ? WriteCallbackResult.Pause : data.Length;
        }

        /// <summary><c>t753_hd_cb</c>: writes the header line and notes the FTP welcome.</summary>
        /// <param name="header">The header bytes.</param>
        /// <returns>The byte count.</returns>
        public long Header(ReadOnlySpan<byte> header)
        {
            string text = System.Text.Encoding.Latin1.GetString(header);
            error.Write($"[{name}] hd_cb '{text}'\n");
            SeenWelcome |= text == "230 Welcome you silly person\r\n";
            return header.Length;
        }
    }

    // One run of the program: its handles and their states.
    private sealed class Run(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, LibTestDebugTrace trace)
    {
        private CurlEasy? first;
        private CurlEasy? second;

        public async ValueTask ExecuteAsync(CancellationToken cancellationToken)
        {
            clock.StartTestTiming();
            context.StandardError.Write("init multi\n");
            CurlMulti? multi = library.IsCleanedUp ? null : library.MultiInit();
            if (multi is null)
            {
                steps.Result = CurlCode.OutOfMemory;
            }
            else
            {
                await using (multi.ConfigureAwait(false))
                {
                    await StartAndLoopAsync(multi, cancellationToken).ConfigureAwait(false);
                    await CleanUpAsync().ConfigureAwait(false);
                }
            }

            if (steps.Result != CurlCode.Ok)
            {
                context.StandardError.Write($"ERROR: {CurlErrorText.Easy(steps.Result)}\n");
            }
        }

        private async ValueTask StartAndLoopAsync(CurlMulti multi, CancellationToken cancellationToken)
        {
            var status = new TransferStatus("EASY1", context.StandardError);
            if (!Setup(status, out first) || !steps.MultiAddHandle(multi, first))
            {
                return;
            }

            var (goesOn, running) = await PerformAsync(multi, cancellationToken).ConfigureAwait(false);
            while (goesOn && running > 0)
            {
                (goesOn, running) = await TurnAsync(multi, status, cancellationToken).ConfigureAwait(false);
            }
        }

        // One pass of the while loop: the swap once EASY1 saw the welcome, a 1 ms wait, a perform.
        private async ValueTask<(bool GoesOn, int Running)> TurnAsync(CurlMulti multi, TransferStatus firstStatus, CancellationToken cancellationToken)
        {
            if (first is not null && firstStatus.SeenWelcome && !await SwapAsync(multi).ConfigureAwait(false))
            {
                return (false, 0);
            }

            var (code, _) = await multi.WaitAsync(Memory<Curl.Api.EngineContracts.Multi.CurlWaitSocket>.Empty, 1, cancellationToken).ConfigureAwait(false);
            if (code != CurlMultiCode.Ok)
            {
                context.StandardError.Write($"curl_multi_wait() returned {(int)code}\n");
                steps.Result = LibTestErrorCodes.MajorBad;
                return (false, 0);
            }

            if (!steps.AbortOnTestTimeout(clock))
            {
                return (false, 0);
            }

            var (goesOn, running) = await PerformAsync(multi, cancellationToken).ConfigureAwait(false);
            return (goesOn && steps.AbortOnTestTimeout(clock), running);
        }

        // EASY1 cleaned up while still in the multi handle, then EASY2, which does not pause, added.
        private async ValueTask<bool> SwapAsync(CurlMulti multi)
        {
            await first!.DisposeAsync().ConfigureAwait(false);
            first = null;

            // Upstream adds EASY2 only when it has none yet; EASY1 is cleaned up once, so this runs once.
            var status = new TransferStatus("EASY2", context.StandardError);
            if (!Setup(status, out second))
            {
                return false;
            }

            status.Pause = false;
            return steps.MultiAddHandle(multi, second);
        }

        // multi_perform and, when it goes on, its line.
        private async ValueTask<(bool GoesOn, int Running)> PerformAsync(CurlMulti multi, CancellationToken cancellationToken)
        {
            var (goesOn, running) = await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false);
            if (goesOn)
            {
                context.StandardError.Write($"multi_perform() -> {running} running\n");
            }

            return (goesOn, running);
        }

        // t753_setup: a handle with the status's callbacks (their closures are its WRITEDATA and HEADERDATA), no progress meter and the trace.
        private bool Setup(TransferStatus status, out CurlEasy? easy)
        {
            if (!steps.EasyInit(library, out easy))
            {
                return false;
            }

            _ = steps.EasySetOptions(easy, [
                (CurlOption.Url, context.Url),
                (CurlOption.Writefunction, (WriteCallback)status.Write),
                (CurlOption.Headerfunction, (HeaderCallback)status.Header),
                (CurlOption.Noprogress, 1L),
                (CurlOption.Debugdata, trace),
                (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                (CurlOption.Verbose, 1L),
            ]);
            return true;
        }

        private async ValueTask CleanUpAsync()
        {
            if (first is not null)
            {
                await first.DisposeAsync().ConfigureAwait(false);
            }

            if (second is not null)
            {
                await second.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
