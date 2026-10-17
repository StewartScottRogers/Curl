using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1515.c</c> (cases 1515 and 1516): two requests through one multi handle to
/// <c>http://testserver.example.com:&lt;port&gt;/&lt;path&gt;&lt;nnnn&gt;</c> (<c>libtest_arg3</c> and <c>argv[1]</c>),
/// each on its own verbose, traced easy handle given the resolve entry
/// <c>testserver.example.com:&lt;port&gt;:&lt;address&gt;</c> and a one-second <c>CURLOPT_DNS_CACHE_TIMEOUT</c>, with a
/// two-second wait between them, so the second must resolve from the manual entry again. A failing request writes
/// <c>request &lt;url&gt; failed with N</c> on stderr and ends the program with its code.
/// </summary>
/// <param name="timeProvider">Where the program's waits, trace times and hang timeout are measured from.</param>
public sealed class Lib1515(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>DNS_TIMEOUT</c>: the DNS cache timeout in seconds.</summary>
    public const long DnsTimeoutSeconds = 1;

    /// <summary>The requests the program makes.</summary>
    public const int RequestCount = 2;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1515";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, timeProvider, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="timeProvider">Where the trace's times come from.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(clock);
        _ = CurlGlobal.Trace("all");
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            var request = new Request(context, steps, library, multi, clock, timeProvider);
            for (int i = 1; i <= RequestCount; i++)
            {
                string url = $"http://testserver.example.com:{context.Argument3}/{context.Url}{i:D4}";
                if (!await request.PerformAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    context.StandardError.Write($"request {url} failed with {(int)steps.Result}\n");
                    return;
                }

                if (i < RequestCount)
                {
                    await clock.WaitAsync((int)((DnsTimeoutSeconds + 1) * 1000), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// The <c>CURLMSG_DONE</c> code for <paramref name="easy"/>: messages are read until one for it, and when none is
    /// found, <paramref name="otherwise"/>. <c>CURLMSG_DONE</c> is the only message type, so the type is not checked.
    /// </summary>
    /// <param name="multi">The multi handle.</param>
    /// <param name="easy">The easy handle.</param>
    /// <param name="otherwise">The code when no message is for <paramref name="easy"/>.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public static CurlCode DoneResult(CurlMulti multi, CurlEasy easy, CurlCode otherwise)
    {
        ArgumentNullException.ThrowIfNull(multi);
        while (multi.InfoRead(out _) is { } message)
        {
            if (message.Easy == easy)
            {
                return message.Result;
            }
        }

        return otherwise;
    }

    /// <summary><c>do_one_request</c>: one traced request through the program's multi handle.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="multi">The program's multi handle.</param>
    /// <param name="clock">The program's clock.</param>
    /// <param name="timeProvider">Where the trace's times come from.</param>
    private sealed class Request(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlMulti multi, LibTestClock clock, TimeProvider timeProvider)
    {
        public async ValueTask<bool> PerformAsync(string url, CancellationToken cancellationToken)
        {
            CurlSlist resolve = CurlSlist.Append(null, $"testserver.example.com:{context.Argument3}:{context.Argument2}");
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return false;
            }

            await using (easy.ConfigureAwait(false))
            {
                var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
                if (steps.SetOptionsAndAddHandle(
                        easy,
                        [
                            (CurlOption.Writedata, context.StandardOutput),
                            (CurlOption.Url, url),
                            (CurlOption.Resolve, resolve),
                            (CurlOption.DnsCacheTimeout, DnsTimeoutSeconds),
                            (CurlOption.Debugdata, trace),
                            (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                            (CurlOption.Verbose, 1L),
                        ],
                        multi)
                    && await steps.PerformUntilDoneAsync(multi, clock, 1000, cancellationToken).ConfigureAwait(false))
                {
                    steps.Result = DoneResult(multi, easy, steps.Result);
                }

                multi.RemoveHandle(easy);
            }

            CurlSlist.FreeAll(resolve);
            return steps.Result == CurlCode.Ok;
        }
    }
}
