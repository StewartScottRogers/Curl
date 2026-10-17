using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;
using Libcurl.Share;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1905.c</c>: a handle on a share handle sharing cookies (set twice) fetches the case's URL through a multi handle,
/// reading and writing the cookies in <c>libtest_arg2</c>, its loop a <c>curl_multi_perform</c> and a wait until no
/// transfer is left; then <c>CURLOPT_COOKIELIST</c> <c>FLUSH</c> writes the shared cookies to that file before the handle
/// leaves the share. The program ends with <c>CURLE_OK</c> whatever the transfer did, and with
/// <see cref="LibTestErrorCodes.Multi"/> when the multi handle cannot be created. Upstream's
/// <c>fdset</c> and <c>select()</c> are a <c>curl_multi_wait</c> for the handle's timeout (1 ms when it has none), and
/// Libcurl's <c>curl_share_init</c> and <c>curl_easy_init</c> fail only on a cleaned-up library.
/// </summary>
public sealed class Lib1905 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1905";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            steps.Result = LibTestErrorCodes.Multi;
            return;
        }

        CurlMulti multi = library.MultiInit();
        await using (multi.ConfigureAwait(false))
        {
            CurlShare share = library.ShareInit();
            _ = share.SetOption(CurlShareOption.Share, CurlLockData.Cookie);
            _ = share.SetOption(CurlShareOption.Share, CurlLockData.Cookie);
            CurlEasy easy = library.EasyInit();
            await using (easy.ConfigureAwait(false))
            {
                _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
                _ = easy.SetOption(CurlOption.Share, share);
                _ = easy.SetOption(CurlOption.Url, context.Url);
                _ = easy.SetOption(CurlOption.Cookiefile, context.Argument2);
                _ = easy.SetOption(CurlOption.Cookiejar, context.Argument2);
                _ = multi.AddHandle(easy);
                await LoopAsync(multi, cancellationToken).ConfigureAwait(false);
                _ = easy.SetOption(CurlOption.Cookielist, "FLUSH");
                _ = easy.SetOption(CurlOption.Share, null);
                _ = multi.RemoveHandle(easy);
            }

            _ = share.Cleanup();
        }

        steps.Result = CurlCode.Ok;
    }

    /// <summary>The program's loop: <c>curl_multi_perform</c>, then a wait for the handle's timeout, until no transfer is left.</summary>
    /// <param name="multi">The multi handle.</param>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <returns>A task that completes when no transfer is left.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public static async ValueTask LoopAsync(CurlMulti multi, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(multi);
        var (_, unfinished) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
        while (unfinished > 0)
        {
            long timeout = multi.Timeout().TimeoutMilliseconds;
            _ = await multi.WaitAsync(default, timeout > 0 ? (int)timeout : 1, cancellationToken).ConfigureAwait(false);
            (_, unfinished) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
