using System.Globalization;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib573.c</c>: a download with headers on through a multi handle's <c>select()</c> loop of 100 ms, traced by
/// <c>libtest_debug_cb</c> with times and no hex; once it is done, a <c>CURLINFO_CONNECT_TIME</c> under the machine
/// epsilon writes <c>connect time %e is &lt; epsilon %e</c> and answers <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
/// <param name="timeProvider">The program's clock, for <c>abort_on_test_timeout</c> and the trace's times.</param>
public sealed class Lib573(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary><c>dbl_epsilon</c>: the smallest double whose half added to 1 still changes 1, 2 to the power -52.</summary>
    public static readonly double DoubleEpsilon = Math.BitIncrement(1.0) - 1.0;

    /// <inheritdoc/>
    public string ProgramName => "lib573";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
        return LibTestRun.WithMacroLibraryAsync(
            context,
            (steps, library) => LibTestRun.SingleMultiTransferAsync(
                steps,
                library,
                clock,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Header, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Debugdata, trace),
                    (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                    (CurlOption.Verbose, 1L),
                ],
                100,
                cancellationToken,
                easy => CheckConnectTime(easy, steps, context.StandardError)));
    }

    /// <summary>Reads <c>CURLINFO_CONNECT_TIME</c> and fails the program when it is under <see cref="DoubleEpsilon"/>.</summary>
    /// <param name="easy">The handle, its transfer done.</param>
    /// <param name="steps">The program's steps.</param>
    /// <param name="error">The program's stderr.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void CheckConnectTime(CurlEasy easy, LibTestSteps steps, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(error);
        easy.GetInfo(CurlInfo.ConnectTime, out object? value);
        double connectTime = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (connectTime < DoubleEpsilon)
        {
            error.Write($"connect time {Exponent(connectTime)} is < epsilon {Exponent(DoubleEpsilon)}\n");
            steps.Result = LibTestErrorCodes.MajorBad;
        }
    }

    /// <summary>C's <c>%e</c>: six decimals and an exponent of at least two digits.</summary>
    private static string Exponent(double value) => value.ToString("0.000000e+00", CultureInfo.InvariantCulture);
}
