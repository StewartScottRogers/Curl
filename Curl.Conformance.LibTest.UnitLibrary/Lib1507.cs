using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1507.c</c>: a verbose SMTP upload from <c>&lt;1507-realuser@example.com&gt;</c> to
/// <c>&lt;1507-recipient@example.com&gt;</c> whose read callback aborts at once, through a multi handle driven by
/// <c>curl_multi_perform</c> and a <c>select()</c> for <c>curl_multi_timeout</c>'s timeout (one second when it gives
/// none or two seconds or more). Its options, performs and timeouts are unchecked, as upstream leaves them; after 60
/// seconds it writes <c>ABORTING TEST, since it seems that it would have run forever.</c> and stops waiting. A failing
/// wait (upstream's <c>select()</c> error) skips that turn's perform. The program answers <see cref="CurlCode.Ok"/>
/// unless <c>easy_init</c> or <c>multi_init</c> failed.
/// </summary>
/// <param name="timeProvider">Where the 60-second limit is measured from.</param>
public sealed class Lib1507(TimeProvider timeProvider) : ILibTest
{
    /// <summary><c>MULTI_PERFORM_HANG_TIMEOUT</c>: how long the loop runs before giving up.</summary>
    public const long MultiPerformHangTimeoutMilliseconds = 60 * 1000;

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1507";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, new LibTestClock(timeProvider), cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock; the loop's start is taken from it.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(clock);
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
                CurlSlist recipients = CurlSlist.Append(null, "<1507-recipient@example.com>");
                easy.SetOption(CurlOption.Writedata, context.StandardOutput);
                easy.SetOption(CurlOption.Url, context.Url);
                easy.SetOption(CurlOption.Upload, 1L);
                easy.SetOption(CurlOption.Readfunction, (ReadCallback)(_ => ReadCallbackResult.Abort));
                easy.SetOption(CurlOption.MailFrom, "<1507-realuser@example.com>");
                easy.SetOption(CurlOption.MailRcpt, recipients);
                easy.SetOption(CurlOption.Verbose, 1L);
                if (steps.MultiAddHandle(multi, easy))
                {
                    await LoopAsync(context, multi, clock, cancellationToken).ConfigureAwait(false);
                }

                CurlSlist.FreeAll(recipients);
                multi.RemoveHandle(easy);
            }
        }
    }

    /// <summary>The <c>select()</c> timeout for <c>curl_multi_timeout</c>'s answer: one second for none (-1) or two seconds or more.</summary>
    /// <param name="multiTimeout">The milliseconds <c>curl_multi_timeout</c> gave.</param>
    /// <returns>The milliseconds to wait.</returns>
    public static int SelectTimeout(long multiTimeout) => multiTimeout is >= 0 and < 2000 ? (int)multiTimeout : 1000;

    private static async ValueTask LoopAsync(LibTestContext context, CurlMulti multi, LibTestClock clock, CancellationToken cancellationToken)
    {
        long start = clock.Now();
        var (_, running) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
        while (running > 0)
        {
            var (_, timeout) = multi.Timeout();
            var (code, _) = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, SelectTimeout(timeout), cancellationToken).ConfigureAwait(false);
            if (clock.DifferenceMilliseconds(clock.Now(), start) > MultiPerformHangTimeoutMilliseconds)
            {
                context.StandardError.Write("ABORTING TEST, since it seems that it would have run forever.\n");
                return;
            }

            if (code == CurlMultiCode.Ok)
            {
                (_, running) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
