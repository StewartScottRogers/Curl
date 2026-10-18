using System.Globalization;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3033.c</c>: three verbose transfers of the case's URL on one reset handle through one multi handle, traced by
/// <c>libtest_debug_cb</c> without hex and with times. Before the second, <c>CURLMOPT_NETWORK_CHANGED</c> clears the
/// pool's connections, so it must make a new connection, and the third must reuse it; each writes <c>[N] signal network
/// change</c> or <c>[N] no network change</c> on stdout first. A failing <c>curl_multi_wait</c>, a failed transfer or a
/// wrong connection count ends the program; the first two with their line on stderr, the last on stdout.
/// </summary>
/// <param name="timeProvider">The time the trace lines carry.</param>
public sealed class Lib3033(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib3033";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, timeProvider, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="timeProvider">The time the trace lines carry.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return;
            }

            await using (easy.ConfigureAwait(false))
            {
                var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
                for (int index = 0; index < 3 && steps.Result == CurlCode.Ok; index++)
                {
                    steps.Result = await RequestAsync(context, multi, easy, trace, index, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary><c>t3033_req_test</c>: one transfer, its network change signalled first when <paramref name="index"/> is 1.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="multi">The multi handle.</param>
    /// <param name="easy">The handle, reset first.</param>
    /// <param name="trace">The program's debug trace.</param>
    /// <param name="index">The transfer's number, 0 to 2.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The transfer's code, or <see cref="LibTestErrorCodes.MajorBad"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<CurlCode> RequestAsync(LibTestContext context, CurlMulti multi, CurlEasy easy, LibTestDebugTrace trace, int index, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(multi);
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(trace);
        if (index == 1)
        {
            _ = multi.SetOption(CurlMultiOption.NetworkChanged, (long)CurlNetworkChange.ClearConnections);
            WriteOutput(context, "[1] signal network change\n");
        }
        else
        {
            WriteOutput(context, string.Create(CultureInfo.InvariantCulture, $"[{index}] no network change\n"));
        }

        easy.Reset();
        _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
        _ = easy.SetOption(CurlOption.Url, context.Url);
        _ = easy.SetOption(CurlOption.Debugdata, trace);
        _ = easy.SetOption(CurlOption.Debugfunction, (DebugCallback)trace.Trace);
        _ = easy.SetOption(CurlOption.Verbose, 1L);
        _ = multi.AddHandle(easy);
        CurlCode result = await RunUntilDoneAsync(context, multi, cancellationToken).ConfigureAwait(false);
        if (result == CurlCode.Ok)
        {
            result = CheckMessages(context, multi, easy, index);
        }

        _ = multi.RemoveHandle(easy);
        return result;
    }

    private static async ValueTask<CurlCode> RunUntilDoneAsync(LibTestContext context, CurlMulti multi, CancellationToken cancellationToken)
    {
        int running;
        do
        {
            (_, running) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
            var (code, _) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, (int)LibTestClock.TestHangTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            if (code != CurlMultiCode.Ok)
            {
                context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"curl_multi_wait() returned {(int)code}\n"));
                return LibTestErrorCodes.MajorBad;
            }
        }
        while (running > 0);

        return CurlCode.Ok;
    }

    private static CurlCode CheckMessages(LibTestContext context, CurlMulti multi, CurlEasy easy, int index)
    {
        while (multi.InfoRead(out _) is { } message)
        {
            if (message.Type != CurlMultiMessageType.Done)
            {
                continue;
            }

            if (message.Result != CurlCode.Ok)
            {
                context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"curl_multi_info_read() returned {(int)message.Result}\n"));
                return message.Result;
            }

            CurlCode result = CheckConnections(context, easy, index);
            if (result != CurlCode.Ok)
            {
                return result;
            }
        }

        return CurlCode.Ok;
    }

    private static CurlCode CheckConnections(LibTestContext context, CurlEasy easy, int index)
    {
        _ = easy.GetInfo(CurlInfo.NumConnects, out object? value);
        long connects = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        string? failure = (index, connects) switch
        {
            (1, 0) => "[1] should not reuse connection in pool\n",
            (2, > 0) => "[2] should have reused connection from [1]\n",
            _ => null,
        };
        if (failure is null)
        {
            return CurlCode.Ok;
        }

        WriteOutput(context, failure);
        return LibTestErrorCodes.MajorBad;
    }

    private static void WriteOutput(LibTestContext context, string text) => context.StandardOutput.Write(Encoding.ASCII.GetBytes(text));
}
