using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib504.c</c>: one URL through a multi handle, through the proxy the second argument names when given,
/// verbose on, driven by <c>multi_perform</c> and <c>curl_multi_wait</c> until nothing runs. It then expects
/// exactly one message from <c>curl_multi_info_read</c> and answers <see cref="LibTestErrorCodes.Success"/>
/// (41) when there is, else <see cref="LibTestErrorCodes.Failure"/>; a failing wait writes
/// <c>curl_multi_wait() returned N</c> to <c>stdout</c> and answers <see cref="LibTestErrorCodes.MajorBad"/>.
/// The upstream loop's code after the wait loop, which runs only while transfers run, is dead and left out.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib504(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib504";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>, ending with cleanup type PA.</summary>
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
                if (steps.SetOptionsAndAddHandle(easy, OptionsOf(context), multi))
                {
                    await RunUntilDoneAsync(context, steps, multi, clock, cancellationToken).ConfigureAwait(false);
                }

                multi.RemoveHandle(easy);
            }
        }
    }

    private static List<(CurlOption Option, object? Value)> OptionsOf(LibTestContext context)
    {
        List<(CurlOption Option, object? Value)> options = context.Argument2 is { } proxy ? [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Proxy, proxy)] : [(CurlOption.Writedata, context.StandardOutput)];
        options.Add((CurlOption.Url, context.Url));
        options.Add((CurlOption.Verbose, 1L));
        return options;
    }

    private static async ValueTask RunUntilDoneAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, LibTestClock clock, CancellationToken cancellationToken)
    {
        context.StandardError.Write("curl_multi_perform()\n");
        var (goesOn, running) = await steps.PerformAndCheckTimeoutAsync(multi, clock, cancellationToken).ConfigureAwait(false);
        while (goesOn && running > 0)
        {
            (goesOn, running) = await WaitThenPerformAsync(context, steps, multi, clock, cancellationToken).ConfigureAwait(false);
        }

        if (goesOn)
        {
            CurlMessage? message = multi.InfoRead(out int messagesLeft);
            context.StandardError.Write("Expected: not running\n");
            steps.Result = message is not null && messagesLeft == 0 ? LibTestErrorCodes.Success : LibTestErrorCodes.Failure;
        }
    }

    // One turn of the inner loop: curl_multi_wait, abort_on_test_timeout, multi_perform, abort_on_test_timeout.
    private static async ValueTask<(bool GoesOn, int RunningHandles)> WaitThenPerformAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, LibTestClock clock, CancellationToken cancellationToken)
    {
        var (code, _) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, (int)LibTestClock.TestHangTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        if (code != CurlMultiCode.Ok)
        {
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.WriteLine($"curl_multi_wait() returned {(int)code}");
            steps.Result = LibTestErrorCodes.MajorBad;
            return (false, 0);
        }

        return steps.AbortOnTestTimeout(clock)
            ? await steps.PerformAndCheckTimeoutAsync(multi, clock, cancellationToken).ConfigureAwait(false)
            : (false, 0);
    }
}
