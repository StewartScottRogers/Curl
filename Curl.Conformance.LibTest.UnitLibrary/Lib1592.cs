using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1592.c</c>: a verbose transfer of the case's URL added to a multi handle, one <c>curl_multi_perform</c>, then
/// <c>curl_multi_remove_handle</c> timed against the hang timeout. Its <c>CURLOPT_TIMEOUT_MS</c> is twice the hang timeout
/// when <c>CURLOPT_DNS_SERVERS</c> can be set (a resolver that can be cancelled) and half of it otherwise, after
/// <see cref="NoDnsServersLine"/> on stderr. The steps are written on stderr around each call.
/// </summary>
/// <param name="timeProvider">Where the program's removal time is measured from.</param>
public sealed class Lib1592(TimeProvider timeProvider) : ILibTest
{
    /// <summary>The line written when <c>CURLOPT_DNS_SERVERS</c> cannot be set.</summary>
    public const string NoDnsServersLine = "CURLOPT_DNS_SERVERS not supported; assuming curl_multi_remove_handle() does block\n";

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1592";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        return LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The program's clock, started before the removal.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(clock);
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
                // Libcurl cannot fail these options on a live handle, so their easy_setopt checks are left out.
                _ = steps.EasySetOptions(easy, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Verbose, 1L), (CurlOption.Url, context.Url)]);
                _ = steps.EasySetOptions(easy, [(CurlOption.TimeoutMs, TimeoutFor(easy.SetOption(CurlOption.DnsServers, "0.0.0.0"), context.StandardError))]);
                if (steps.MultiAddHandle(multi, easy))
                {
                    await PerformAndRemoveAsync(context, steps, multi, easy, clock, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// The program's <c>CURLOPT_TIMEOUT_MS</c> for <c>curl_easy_setopt(CURLOPT_DNS_SERVERS)</c>'s answer: twice the hang
    /// timeout when it was set, else half of it after <see cref="NoDnsServersLine"/> on <paramref name="error"/>.
    /// </summary>
    /// <param name="dnsServersAnswer">What setting <c>CURLOPT_DNS_SERVERS</c> answered.</param>
    /// <param name="error">The program's stderr.</param>
    /// <returns>The timeout in milliseconds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static long TimeoutFor(CurlCode dnsServersAnswer, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (dnsServersAnswer == CurlCode.Ok)
        {
            return LibTestClock.TestHangTimeoutMilliseconds * 2;
        }

        error.Write(NoDnsServersLine);
        return LibTestClock.TestHangTimeoutMilliseconds / 2;
    }

    private static async ValueTask PerformAndRemoveAsync(LibTestContext context, LibTestSteps steps, CurlMulti multi, CurlEasy easy, LibTestClock clock, CancellationToken cancellationToken)
    {
        context.StandardError.Write("curl_multi_perform()...\n");
        if (!(await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false)).GoesOn)
        {
            return;
        }

        context.StandardError.Write("curl_multi_perform() succeeded\n");
        context.StandardError.Write("curl_multi_remove_handle()...\n");
        clock.StartTestTiming();
        var code = multi.RemoveHandle(easy);
        if (code != CurlMultiCode.Ok)
        {
            context.StandardError.Write($"curl_multi_remove_handle() failed, with code {(int)code}\n");
            steps.Result = LibTestErrorCodes.Multi;
            return;
        }

        context.StandardError.Write("curl_multi_remove_handle() succeeded\n");
        _ = steps.AbortOnTestTimeout(clock);
    }
}
