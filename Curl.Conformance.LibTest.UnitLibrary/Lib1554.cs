using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Libcurl.Share;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1554.c</c>: a share handle sharing the connection cache, with lock and unlock callbacks that write
/// <c>-] Mutex lock NAME</c> and <c>[- Mutex unlock NAME</c> on stdout, and three GETs, each on a new easy handle
/// using the share and cleaned up after its perform, so the second and third reuse the pooled connection. A failing
/// perform writes <c>curl_easy_perform() failed: TEXT</c> on stderr and ends the loop with its code; a share handle
/// that cannot be created writes <c>curl_share_init() failed</c> on stderr and ends with <c>CURLE_OK</c>.
/// </summary>
public sealed class Lib1554 : ILibTest
{
    /// <summary>How many transfers the program makes.</summary>
    public const int Transfers = 3;

    /// <summary><c>ldata_names</c>: each <see cref="CurlLockData"/>'s name, by number.</summary>
    public static IReadOnlyList<string> LockDataNames { get; } = ["NONE", "SHARE", "COOKIE", "DNS", "SESSION", "CONNECT", "PSL", "HSTS", "NULL"];

    /// <inheritdoc/>
    public string ProgramName => "lib1554";

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
        ArgumentNullException.ThrowIfNull(library);
        CurlShare? share = library.IsCleanedUp ? null : library.ShareInit();
        if (share is null)
        {
            context.StandardError.Write("curl_share_init() failed\n");
            return;
        }

        _ = share.SetOption(CurlShareOption.Share, CurlLockData.Connect);
        _ = share.SetOption(CurlShareOption.LockFunction, (ShareLockCallback)((_, data, _) => WriteLine(context, $"-] Mutex lock {LockDataNames[(int)data]}\n")));
        _ = share.SetOption(CurlShareOption.UnlockFunction, (ShareUnlockCallback)((_, data) => WriteLine(context, $"[- Mutex unlock {LockDataNames[(int)data]}\n")));
        for (int i = 0; i < Transfers && steps.Result == CurlCode.Ok; i++)
        {
            await TransferAsync(context, steps, library, share, cancellationToken).ConfigureAwait(false);
        }

        _ = share.Cleanup();
    }

    /// <summary>
    /// One lap: a new easy handle using <paramref name="share"/>, performed and cleaned up; a failing perform writes
    /// its line. An easy handle that cannot be created skips the lap.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps; <see cref="LibTestSteps.Result"/> becomes the perform's code.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="share">The share handle.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the lap's handle is cleaned up.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TransferAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CurlShare share, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Share, share);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        }

        if (steps.Result != CurlCode.Ok)
        {
            context.StandardError.Write($"curl_easy_perform() failed: {CurlErrorText.Easy(steps.Result)}\n");
        }
    }

    private static void WriteLine(LibTestContext context, string line)
    {
        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        output.Write(line);
    }
}
