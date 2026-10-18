using System.Runtime.CompilerServices;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Misc.ErrorText;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The <c>select_test</c> step of curl 8.22.0's <c>tests/libtest/first.h</c> and the multi loop the
/// <c>select()</c>-driven programs (<c>lib502.c</c>, <c>lib503.c</c>, <c>lib507.c</c>) share.
/// </summary>
public sealed partial class LibTestSteps
{
    /// <summary>
    /// <c>multi_fdset</c> then <c>select_test(maxfd + 1, &amp;rd, &amp;wr, &amp;exc, &amp;timeout)</c>. The managed
    /// multi handle's sockets are no descriptors <c>select()</c> could wait on, so the wait is
    /// <c>curl_multi_wait</c> on the same handle for the same timeout, which ends as soon as a transfer has
    /// something to do, as the <c>select()</c> would. A failing wait writes
    /// <c>&lt;file&gt;:&lt;line&gt; select() failed, with code N (text)</c> and <see cref="Result"/> becomes
    /// <see cref="LibTestErrorCodes.Select"/>.
    /// </summary>
    /// <param name="multi">The handle.</param>
    /// <param name="timeoutMilliseconds">The <c>select()</c> timeout.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> SelectTestAsync(
        CurlMulti multi,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        if (!MultiFdSet(multi, new HashSet<CurlSocket>(), new HashSet<CurlSocket>(), new HashSet<CurlSocket>(), out _, file, line))
        {
            return false;
        }

        var (code, _) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        if (code == CurlMultiCode.Ok)
        {
            return true;
        }

        Result = LibTestErrorCodes.Select;
        WriteLine(file, line, $"select() failed, with code {(int)code} ({CurlErrorText.Multi(code)})");
        return false;
    }

    /// <summary>
    /// The loop of <c>lib502.c</c>, <c>lib503.c</c> and <c>lib507.c</c>: <c>multi_perform</c> and
    /// <c>abort_on_test_timeout</c> until nothing runs, with <see cref="SelectTestAsync"/> and
    /// <c>abort_on_test_timeout</c> between.
    /// </summary>
    /// <param name="multi">The handle, its transfers added.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="selectTimeoutMilliseconds">The <c>select()</c> timeout of each turn.</param>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on: every transfer finished and no step failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> or <paramref name="clock"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> PerformUntilDoneAsync(
        CurlMulti multi,
        LibTestClock clock,
        int selectTimeoutMilliseconds,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        ArgumentNullException.ThrowIfNull(clock);
        while (true)
        {
            var (goesOn, running) = await PerformAndCheckTimeoutAsync(multi, clock, cancellationToken, file, line).ConfigureAwait(false);
            if (!goesOn)
            {
                return false;
            }

            if (running == 0)
            {
                return true;
            }

            if (!await SelectTestAsync(multi, selectTimeoutMilliseconds, cancellationToken, file, line).ConfigureAwait(false)
                || !AbortOnTestTimeout(clock, file: file, line: line))
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The loop of <c>lib591.c</c> and <c>lib597.c</c>: <c>multi_perform</c> and <c>abort_on_test_timeout</c> until
    /// nothing runs, with <c>multi_timeout</c>, <see cref="SelectTestAsync"/> for that timeout (or
    /// <paramref name="noTimeoutMilliseconds"/> when it is -1, capped at <see cref="int.MaxValue"/>) and
    /// <c>abort_on_test_timeout</c> between.
    /// </summary>
    /// <param name="multi">The handle, its transfers added.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="noTimeoutMilliseconds">The <c>select()</c> timeout when <c>curl_multi_timeout</c> gives -1.</param>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on: every transfer finished and no step failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> or <paramref name="clock"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> PerformWithMultiTimeoutUntilDoneAsync(
        CurlMulti multi,
        LibTestClock clock,
        int noTimeoutMilliseconds,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(clock);
        while (true)
        {
            var (goesOn, running) = await PerformAndCheckTimeoutAsync(multi, clock, cancellationToken, file, line).ConfigureAwait(false);
            if (!goesOn || running == 0)
            {
                return goesOn;
            }

            if (!MultiTimeout(multi, out long timeout, file, line))
            {
                return false;
            }

            int wait = timeout == -1 ? noTimeoutMilliseconds : (int)Math.Min(timeout, int.MaxValue);
            if (!await SelectTestAsync(multi, wait, cancellationToken, file, line).ConfigureAwait(false)
                || !AbortOnTestTimeout(clock, file: file, line: line))
            {
                return false;
            }
        }
    }

    /// <summary><c>multi_perform(multi, &amp;running)</c> then <c>abort_on_test_timeout()</c>, as the multi programs call them in pairs.</summary>
    /// <param name="multi">The handle.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on, and the running count the perform gave.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> or <paramref name="clock"/> is <see langword="null"/>.</exception>
    public async ValueTask<(bool GoesOn, int RunningHandles)> PerformAndCheckTimeoutAsync(
        CurlMulti multi,
        LibTestClock clock,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var (goesOn, running) = await MultiPerformAsync(multi, cancellationToken, file, line).ConfigureAwait(false);
        return (goesOn && AbortOnTestTimeout(clock, file: file, line: line), running);
    }
}
