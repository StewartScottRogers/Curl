using System.Runtime.CompilerServices;

namespace Curl.Conformance.LibTest;

/// <summary>The hang-timeout step of curl 8.22.0's <c>tests/libtest/first.h</c>.</summary>
public sealed partial class LibTestSteps
{
    /// <summary>
    /// <c>abort_on_test_timeout()</c>, or <c>abort_on_test_timeout_custom(T)</c> with
    /// <paramref name="timeoutMilliseconds"/>: when more than the timeout has passed since
    /// <see cref="LibTestClock.StartTestTiming"/>, writes <c>ABORTING TEST, since it seems that it would
    /// have run forever (N ms &gt; 60000 ms)</c> - upstream always names <c>TEST_HANG_TIMEOUT</c>, even
    /// for a custom timeout - and <see cref="Result"/> becomes <see cref="LibTestErrorCodes.RunsForever"/>.
    /// </summary>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="timeoutMilliseconds">The timeout, <see cref="LibTestClock.TestHangTimeoutMilliseconds"/> by default.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is <see langword="null"/>.</exception>
    public bool AbortOnTestTimeout(
        LibTestClock clock,
        long timeoutMilliseconds = LibTestClock.TestHangTimeoutMilliseconds,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(clock);
        long elapsed = clock.MillisecondsSinceTestStart();
        if (elapsed <= timeoutMilliseconds)
        {
            return true;
        }

        Result = LibTestErrorCodes.RunsForever;
        WriteLine(file, line, $"ABORTING TEST, since it seems that it would have run forever ({elapsed} ms > {LibTestClock.TestHangTimeoutMilliseconds} ms)");
        return false;
    }
}
