using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1540.c</c>: a verbose, time-stamped traced GET of a chunked body with a trailer whose first write call pauses
/// the transfer (<c>Got bytes but pausing!</c> on stdout) and whose transfer-info callback unpauses it on its second call
/// while paused, writing <c>xferinfo: paused N</c> on stderr each time. Headers and the body after the pause go to
/// stdout; the transfer's code is the program's.
/// </summary>
/// <param name="timeProvider">The clock behind <c>start_test_timing</c> and the trace's time stamps.</param>
public sealed class Lib1540(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1540";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        new LibTestClock(timeProvider).StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, timeProvider, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="timeProvider">Where the trace's times come from.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var status = new TransferStatus(easy, context.StandardOutput, context.StandardError);
            var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
            _ = steps.EasySetOptions(easy, [
                (CurlOption.Url, context.Url),
                (CurlOption.Writefunction, (WriteCallback)status.Write),
                (CurlOption.Headerfunction, (HeaderCallback)status.Header),
                (CurlOption.Xferinfofunction, (TransferInfoCallback)status.PleaseContinue),
                (CurlOption.Noprogress, 0L),
                (CurlOption.Debugdata, trace),
                (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                (CurlOption.Verbose, 1L),
            ]);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>struct t1540_transfer_status</c> with its callbacks: whether the body is paused, and the counts.</summary>
    /// <param name="easy">The handle the transfer-info callback unpauses.</param>
    /// <param name="output">The program's stdout.</param>
    /// <param name="error">The program's stderr.</param>
    public sealed class TransferStatus(CurlEasy easy, Stream output, TextWriter error)
    {
        private int counter;
        private int please;

        /// <summary>Whether the first write call has paused the transfer.</summary>
        public bool Halted { get; private set; }

        /// <summary><c>please_continue</c>: counts its calls while halted, unpausing on the second; always goes on.</summary>
        /// <param name="downloadTotal">Unused.</param>
        /// <param name="downloadNow">Unused.</param>
        /// <param name="uploadTotal">Unused.</param>
        /// <param name="uploadNow">Unused.</param>
        /// <returns>0.</returns>
        public int PleaseContinue(long downloadTotal, long downloadNow, long uploadTotal, long uploadNow)
        {
            if (Halted && ++please == 2)
            {
                _ = easy.Pause(CurlPause.Continue);
            }

            error.Write($"xferinfo: paused {(Halted ? 1 : 0)}\n");
            return 0;
        }

        /// <summary><c>t1540_header_callback</c>: the header to stdout.</summary>
        /// <param name="header">The header bytes.</param>
        /// <returns>The byte count.</returns>
        public long Header(ReadOnlySpan<byte> header)
        {
            output.Write(header);
            return header.Length;
        }

        /// <summary><c>t1540_write_cb</c>: the first call pauses, writing <c>Got bytes but pausing!</c> for data; later calls write the body to stdout.</summary>
        /// <param name="data">The body bytes.</param>
        /// <returns>The byte count, or <see cref="WriteCallbackResult.Pause"/> on the first call.</returns>
        public long Write(ReadOnlySpan<byte> data)
        {
            if (++counter > 1)
            {
                output.Write(data);
                return data.Length;
            }

            if (data.Length > 0)
            {
                output.Write(Encoding.ASCII.GetBytes("Got bytes but pausing!\n"));
            }

            Halted = true;
            return WriteCallbackResult.Pause;
        }
    }
}
