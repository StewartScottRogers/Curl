using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Misc.ErrorText;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib540.c</c>: the URL twice through one multi handle, each time on a new easy handle, through the proxy
/// <c>libtest_arg2</c> as the proxy user <c>libtest_arg3</c> with any proxy authentication, headers and verbose on
/// and a custom <c>Host:</c> from the fourth argument. Each transfer is driven by <c>multi_perform</c> and a
/// <c>select()</c> for <c>curl_multi_timeout</c> (5 seconds when none) until it ends; each finished one prints
/// <c>R: code - text</c> on stderr and is removed and cleaned up. Fewer than three arguments answer
/// <see cref="LibTestErrorCodes.MajorBad"/>. Cleanup type PB.
/// </summary>
/// <param name="timeProvider">Where the program's hang timeout is measured from.</param>
public sealed class Lib540(TimeProvider timeProvider) : ILibTest
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib540";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        if (context.Arguments.Count < 3)
        {
            return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
        }

        string host = context.Arguments.Count > 3 ? context.Arguments[3] : "(nil)";
        CurlSlist headers = CurlSlist.Append(null, $"Host: {host}");
        LibTestResult result = await LibTestRun.WithMacroLibraryAsync(
            context,
            (steps, library) => TestAsync(context, steps, library, clock, headers, cancellationToken)).ConfigureAwait(false);
        CurlSlist.FreeAll(headers);
        return result;
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="headers">The custom <c>Host:</c> header.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        LibTestClock clock,
        CurlSlist headers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        var run = new Run(context, steps, library, multi, clock, headers);
        if (await run.LoopAsync(0, cancellationToken).ConfigureAwait(false))
        {
            context.StandardError.Write("lib540: now we do the request again\n");
            await run.LoopAsync(1, cancellationToken).ConfigureAwait(false);
        }

        await run.CleanupAsync().ConfigureAwait(false);
    }

    private sealed class Run(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlMulti multi, LibTestClock clock, CurlSlist headers)
    {
        private readonly CurlEasy?[] handles = new CurlEasy?[2];

        public async ValueTask<bool> LoopAsync(int number, CancellationToken cancellationToken)
        {
            if (!await InitAsync(number).ConfigureAwait(false))
            {
                return false;
            }

            int running = -1;
            while (running != 0)
            {
                (bool goesOn, running) = await steps.PerformAndCheckTimeoutAsync(multi, clock, cancellationToken).ConfigureAwait(false);
                if (!goesOn || (running != 0 && !await WaitAsync(cancellationToken).ConfigureAwait(false)))
                {
                    return false;
                }

                await ReadMessagesAsync().ConfigureAwait(false);
                if (!steps.AbortOnTestTimeout(clock))
                {
                    return false;
                }
            }

            return true;
        }

        public async ValueTask CleanupAsync()
        {
            foreach (CurlEasy? easy in handles)
            {
                if (easy is not null)
                {
                    multi.RemoveHandle(easy);
                    await easy.DisposeAsync().ConfigureAwait(false);
                }
            }

            await multi.DisposeAsync().ConfigureAwait(false);
        }

        private async ValueTask<bool> InitAsync(int number)
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return false;
            }

            if (steps.SetOptionsAndAddHandle(easy, OptionsOf(context, headers), multi))
            {
                handles[number] = easy;
                return true;
            }

            await easy.DisposeAsync().ConfigureAwait(false);
            return false;
        }

        private async ValueTask<bool> WaitAsync(CancellationToken cancellationToken)
        {
            if (!steps.MultiTimeout(multi, out long timeout))
            {
                return false;
            }

            int selectTimeout = timeout == -1 ? 5000 : (int)Math.Min(timeout, int.MaxValue);
            return await steps.SelectTestAsync(multi, selectTimeout, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask ReadMessagesAsync()
        {
            while (multi.InfoRead(out _) is { } message)
            {
                if (message.Type != CurlMultiMessageType.Done)
                {
                    context.StandardError.Write($"E: CURLMsg ({(int)message.Type})\n");
                    continue;
                }

                context.StandardError.Write($"R: {(int)message.Result} - {CurlErrorText.Easy(message.Result)}\n");
                multi.RemoveHandle(message.Easy);
                await message.Easy.DisposeAsync().ConfigureAwait(false);
                handles[Array.IndexOf(handles, message.Easy)] = null;
            }
        }

        private static List<(CurlOption Option, object? Value)> OptionsOf(LibTestContext context, CurlSlist headers) =>
        [
            (CurlOption.Writedata, context.StandardOutput),
            (CurlOption.Url, context.Url),
            (CurlOption.Proxy, context.Argument2),
            (CurlOption.Proxyuserpwd, context.Argument3),
            (CurlOption.Proxyauth, (long)HttpAuthenticationMethods.Any),
            (CurlOption.Verbose, 1L),
            (CurlOption.Header, 1L),
            (CurlOption.Httpheader, headers),
        ];
    }
}
