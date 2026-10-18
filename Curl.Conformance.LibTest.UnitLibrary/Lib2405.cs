using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.Version;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2405.c</c> (cases 2405 and 2407): <c>curl_multi_waitfds</c> on an empty multi handle (no descriptor) and on one
/// holding a handle that has not run (at most one), then two transfers of the case's URL on one multi handle, each turn
/// checking <c>curl_multi_waitfds</c>'s answers and polling the descriptors it gave on a second multi handle for 500 ms;
/// the most descriptors any turn gave must be two (case 2405 over HTTP/1.1, case 2407 over HTTP/2) and then one
/// (case 2407 multiplexed), plus one when libcurl resolves names on a thread. A check that fails writes why and ends
/// with <see cref="LibTestErrorCodes.Failure"/>. Upstream's calls with a <c>NULL</c> <c>ufds</c> and a size or a
/// <c>NULL</c> <c>fd_count</c> are left out, as Libcurl's span and <see langword="out"/> count have no <c>NULL</c>;
/// its zero-size <c>ufds</c> calls are the too-short call of <see cref="CountChecksPass"/>.
/// </summary>
public sealed class Lib2405 : ILibTest
{
    /// <summary><c>TEST_USE_HTTP1</c>.</summary>
    public const int UseHttp1 = 0;

    /// <summary><c>TEST_USE_HTTP2</c>.</summary>
    public const int UseHttp2 = 1;

    /// <summary><c>TEST_USE_HTTP2_MPLEX</c>.</summary>
    public const int UseHttp2Multiplex = 2;

    /// <summary>The length of the program's <c>ufds</c> arrays.</summary>
    public const int WaitSocketCapacity = 10;

    /// <summary><c>CURLPIPE_MULTIPLEX</c>.</summary>
    public const long PipeMultiplex = 2;

    /// <inheritdoc/>
    public string ProgramName => "lib2405";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!await EmptyMultiTestAsync(context, steps, library).ConfigureAwait(false))
        {
            return;
        }

        int threaded = WakeupDescriptorCount(CurlVersion.Info(library));
        if (context.TestNumber == 2405)
        {
            await RunCheckAsync(context, steps, library, UseHttp1, 2 + threaded, cancellationToken).ConfigureAwait(false);
            return;
        }

        await RunCheckAsync(context, steps, library, UseHttp2, 2 + threaded, cancellationToken).ConfigureAwait(false);
        if (steps.Result == CurlCode.Ok)
        {
            await RunCheckAsync(context, steps, library, UseHttp2Multiplex, 1 + threaded, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>uses_threaded</c>: whether libcurl resolves names on a thread, so a wakeup descriptor joins the wait.</summary>
    /// <param name="version">The library's <c>curl_version_info</c>.</param>
    /// <returns>Whether the features name <c>asyn-rr</c>, or name <c>AsynchDNS</c> without c-ares.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> is <see langword="null"/>.</exception>
    public static bool UsesThreadedResolver(CurlVersionInfoData version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return version.FeatureNames.Contains("asyn-rr")
            || ((version.Features & CurlVersionFeatures.AsynchDns) != 0 && version.Ares is null);
    }

    /// <summary>The program's <c>uses_threaded_resolver</c>: the wakeup descriptor a threaded resolver adds to each count.</summary>
    /// <param name="version">The library's <c>curl_version_info</c>.</param>
    /// <returns>1 when <see cref="UsesThreadedResolver"/>, else 0.</returns>
    public static int WakeupDescriptorCount(CurlVersionInfoData version) => UsesThreadedResolver(version) ? 1 : 0;

    /// <summary><c>set_easy</c>: the options both transfers and the empty test's handle get.</summary>
    /// <param name="easy">The handle.</param>
    /// <param name="url">The URL to transfer.</param>
    /// <param name="use">One of <see cref="UseHttp1"/>, <see cref="UseHttp2"/> and <see cref="UseHttp2Multiplex"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public static void SetEasy(CurlEasy easy, string? url, int use)
    {
        ArgumentNullException.ThrowIfNull(easy);
        // Libcurl takes these values without fail, so upstream's easy_setopt checks are left out.
        _ = easy.SetOption(CurlOption.Url, url);
        _ = easy.SetOption(CurlOption.Verbose, 1L);
        _ = easy.SetOption(CurlOption.HttpVersion, (long)(use == UseHttp1 ? HttpVersion.Http11 : HttpVersion.Http2));
        if (use == UseHttp2Multiplex)
        {
            _ = easy.SetOption(CurlOption.Pipewait, 1L);
        }

        _ = easy.SetOption(CurlOption.SslVerifypeer, 0L);
        _ = easy.SetOption(CurlOption.SslVerifyhost, 0L);
        _ = easy.SetOption(CurlOption.Header, 1L);
        _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
    }

    /// <summary>
    /// One turn's checks after a first <c>curl_multi_waitfds</c> gave <paramref name="count"/> descriptors: asking
    /// only for the count gives at least as many, and too short a <c>ufds</c> gives <c>CURLM_OUT_OF_MEMORY</c> and
    /// still the full count.
    /// </summary>
    /// <param name="error">Where a failing check writes why.</param>
    /// <param name="steps">The program's steps; a failing check sets <see cref="LibTestErrorCodes.Failure"/>.</param>
    /// <param name="multi">The multi handle.</param>
    /// <param name="count">What the first call gave, at least 1.</param>
    /// <returns>Whether every check passed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool CountChecksPass(TextWriter error, LibTestSteps steps, CurlMulti multi, int count)
    {
        ArgumentNullException.ThrowIfNull(multi);
        // Upstream's NULL ufds asks only for the count and answers CURLM_OK; Libcurl's empty span asks the same and
        // answers CURLM_OUT_OF_MEMORY when any descriptor is waited on, so either code passes here.
        CurlMultiCode code = multi.WaitFds(Span<CurlWaitSocket>.Empty, out int check);
        if (Failed(error, steps, code is not (CurlMultiCode.Ok or CurlMultiCode.OutOfMemory), $"curl_multi_waitfds() failed, code {(int)code}.\n")
            || Failed(error, steps, check < count, $"curl_multi_waitfds() should return at least the number of fds needed ({check} vs. {count})\n"))
        {
            return false;
        }

        code = multi.WaitFds(new CurlWaitSocket[count - 1], out check);
        return !Failed(error, steps, code != CurlMultiCode.OutOfMemory, $"curl_multi_waitfds() return code {(int)code} instead of CURLM_OUT_OF_MEMORY.\n")
            && !Failed(error, steps, check < count, $"curl_multi_waitfds() should return the amount of fds needed if enough is not passed in ({check} vs. {count}).\n");
    }

    private static bool Failed(TextWriter error, LibTestSteps steps, bool failed, string message)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(steps);
        if (failed)
        {
            error.Write(message);
            steps.Result = LibTestErrorCodes.Failure;
        }

        return failed;
    }

    private static async ValueTask<bool> EmptyMultiTestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return false;
        }

        bool passed = await EmptyChecksPassAsync(context, steps, library, multi).ConfigureAwait(false);
        await multi.DisposeAsync().ConfigureAwait(false);
        return passed;
    }

    private static async ValueTask<bool> EmptyChecksPassAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlMulti multi)
    {
        var sockets = new CurlWaitSocket[WaitSocketCapacity];
        CurlMultiCode code = multi.WaitFds(sockets, out int count);
        if (Failed(context.StandardError, steps, code != CurlMultiCode.Ok, $"curl_multi_waitfds() failed, code {(int)code}.\n")
            || Failed(context.StandardError, steps, count > 0, $"curl_multi_waitfds(), empty, returned non-zero count of waitfds: {count}.\n")
            || !steps.EasyInit(library, out CurlEasy? easy))
        {
            return false;
        }

        SetEasy(easy, "http://example.com", UseHttp1);
        bool passed = steps.MultiAddHandle(multi, easy) && AddedHandleCheckPasses(context, steps, multi, sockets);
        _ = multi.RemoveHandle(easy);
        await easy.DisposeAsync().ConfigureAwait(false);
        return passed;
    }

    private static bool AddedHandleCheckPasses(LibTestContext context, LibTestSteps steps, CurlMulti multi, CurlWaitSocket[] sockets)
    {
        CurlMultiCode code = multi.WaitFds(sockets, out int count);
        return !Failed(context.StandardError, steps, code != CurlMultiCode.Ok, $"curl_multi_waitfds() failed, code {(int)code}.\n")
            && !Failed(context.StandardError, steps, count > 1, $"curl_multi_waitfds() returned > 1 count of waitfds: {count}.\n");
    }

    // test_run_check: the run, then its code or the most descriptors held against what was expected.
    private static async ValueTask RunCheckAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, int use, int expected, CancellationToken cancellationToken)
    {
        int most = await Lib2405Run.RunAsync(context, steps, library, use, cancellationToken).ConfigureAwait(false);
        if (steps.Result != CurlCode.Ok)
        {
            context.StandardError.Write($"test failed with code: {(int)steps.Result}\n");
            return;
        }

        _ = Failed(context.StandardError, steps, most != expected, $"Max number of waitfds: {most} not as expected: {expected}\n");
    }
}
