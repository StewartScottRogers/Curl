using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1553.c</c>: a verbose MIME post of one part (<c>name</c>, <c>value</c>) with headers, the credentials
/// <c>u:s</c>, an xferinfo callback that writes <c>xferinfo fail!</c> on stderr and aborts (left unused, since
/// progress stays off) and <c>libtest_debug_cb</c> tracing with times and no hex, through a multi handle driven by
/// <see cref="Lib1552.WaitLoopAsync"/> for at most one wait. Cleanup frees the MIME before
/// <c>curl_multi_remove_handle</c>, <c>curl_multi_cleanup</c> and <c>curl_easy_cleanup</c>; case 1553 checks that
/// cleanup after a host that does not resolve. The program ends with <c>CURLE_OK</c> unless a step failed.
/// </summary>
/// <param name="timeProvider">The clock behind the hang timeout and the trace's times.</param>
public sealed class Lib1553(TimeProvider timeProvider) : ILibTest
{
    /// <summary>How many <c>curl_multi_wait</c> turns the loop takes at most: <c>counter</c>.</summary>
    public const int WaitTurns = 1;

    /// <summary>The line the xferinfo callback writes on stderr.</summary>
    public const string TransferInfoFailLine = "xferinfo fail!\n";

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1553";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        clock.StartTestTiming();
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, timeProvider, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started by <see cref="LibTestClock.StartTestTiming"/>.</param>
    /// <param name="timeProvider">The clock behind the trace's times.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, TimeProvider timeProvider, CancellationToken cancellationToken)
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
                using (CurlMime mime = library.MimeInit())
                {
                    CurlMimePart field = mime.AddPart();
                    _ = field.SetName("name");
                    _ = field.SetData("value");
                    var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);
                    _ = steps.SetOptionsAndAddHandle(
                            easy,
                            [
                                (CurlOption.Writedata, context.StandardOutput),
                                (CurlOption.Url, context.Url),
                                (CurlOption.Header, 1L),
                                (CurlOption.Verbose, 1L),
                                (CurlOption.Mimepost, mime),
                                (CurlOption.Userpwd, "u:s"),
                                (CurlOption.Xferinfofunction, (TransferInfoCallback)((_, _, _, _) => TransferInfoFail(context))),
                                (CurlOption.Noprogress, 1L),
                                (CurlOption.Debugdata, trace),
                                (CurlOption.Debugfunction, (DebugCallback)trace.Trace),
                                (CurlOption.Verbose, 1L),
                            ],
                            multi)
                        && await Lib1552.WaitLoopAsync(context, steps, multi, clock, WaitTurns, cancellationToken).ConfigureAwait(false);
                }

                _ = multi.RemoveHandle(easy);
                await multi.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary><c>t1553_xferinfo</c>: writes <see cref="TransferInfoFailLine"/> on stderr and fails the transfer.</summary>
    /// <param name="context">The program's context.</param>
    /// <returns>1, which aborts the transfer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static int TransferInfoFail(LibTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.StandardError.Write(TransferInfoFailLine);
        return 1;
    }
}
