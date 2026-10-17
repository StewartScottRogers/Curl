using Curl.Api.Codes;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib661.c</c>: eight verbose FTP transfers checking which <c>CWD</c> commands each
/// <c>CURLOPT_FTP_FILEMETHOD</c> sends - <c>SINGLECWD</c> and <c>NOCWD</c> downloads of absolute paths that must
/// each end <c>CURLE_REMOTE_FILE_NOT_FOUND</c>, on fresh and reused handles, then <c>NOBODY</c> transfers of the
/// home directory with a <c>SYST</c> quote that must succeed, the last after <c>curl_easy_reset</c>. The first
/// broken expectation ends the program, writing <c>test encountered error N</c> on stderr.
/// </summary>
public sealed class Lib661 : ILibTest
{
    /// <summary>How a transfer's handle is reached: a fresh one, the one before, or the one before after a reset.</summary>
    public enum HandleUse
    {
        /// <summary>A new handle from <c>curl_easy_init</c>, the old one cleaned up.</summary>
        Fresh,

        /// <summary>The handle the transfer before used.</summary>
        Reused,

        /// <summary>The handle the transfer before used, after <c>curl_easy_reset</c>.</summary>
        Reset,
    }

    /// <inheritdoc/>
    public string ProgramName => "lib661";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The eight transfers in upstream's order, each with its handle, options and expected answer.</summary>
    /// <param name="context">The program's context; its URL is the server's home directory.</param>
    /// <returns>The transfers.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<(HandleUse Handle, (CurlOption Option, object? Value)[] Options, CurlCode Expected)> Transfers(LibTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string folderA = $"{context.Url}/folderA/661";
        string folderB = $"{context.Url}/folderB/661";
        CurlSlist quote = CurlSlist.Append(null, "SYST");
        const CurlCode NotFound = CurlCode.RemoteFileNotFound;
        return
        [
            (HandleUse.Fresh, Download(context, folderA, FtpFileMethod.SingleCwd), NotFound),
            (HandleUse.Reused, [(CurlOption.Url, folderB)], NotFound),
            (HandleUse.Fresh, Download(context, folderA, FtpFileMethod.NoCwd), NotFound),
            (HandleUse.Reused, [(CurlOption.Url, folderB), (CurlOption.FtpFilemethod, (long)FtpFileMethod.SingleCwd)], NotFound),
            (HandleUse.Reused, [(CurlOption.Url, folderA), (CurlOption.FtpFilemethod, (long)FtpFileMethod.NoCwd)], NotFound),
            (HandleUse.Fresh, Home(context, FtpFileMethod.NoCwd, quote), CurlCode.Ok),
            (HandleUse.Fresh, Home(context, FtpFileMethod.SingleCwd, quote), CurlCode.Ok),
            (HandleUse.Reset, Home(context, FtpFileMethod.NoCwd, quote), CurlCode.Ok),
        ];
    }

    /// <summary>The body after <c>curl_global_init</c>: the transfers until one answers other than expected.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        CurlEasy? easy = await RunTransfersAsync(steps, library, Transfers(context), cancellationToken).ConfigureAwait(false);
        await (easy?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
        if (steps.Result != CurlCode.Ok)
        {
            context.StandardError.Write($"test encountered error {(int)steps.Result}\n");
        }
    }

    // The transfers until one answers other than expected or a fresh handle cannot be had; answers the last handle.
    private static async ValueTask<CurlEasy?> RunTransfersAsync(
        LibTestSteps steps,
        CurlLibrary library,
        IReadOnlyList<(HandleUse Handle, (CurlOption Option, object? Value)[] Options, CurlCode Expected)> transfers,
        CancellationToken cancellationToken)
    {
        CurlEasy? easy = null;
        for (int i = 0; i < transfers.Count && (i == 0 || steps.Result == transfers[i - 1].Expected); i++)
        {
            easy = await HandleForAsync(steps, library, easy, transfers[i].Handle).ConfigureAwait(false);
            if (easy is null)
            {
                return null;
            }

            _ = steps.EasySetOptions(easy, transfers[i].Options);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        }

        return easy;
    }

    // The handle a transfer uses: a fresh one (the old one cleaned up, null when init fails), or the old one, reset or not.
    private static async ValueTask<CurlEasy?> HandleForAsync(LibTestSteps steps, CurlLibrary library, CurlEasy? easy, HandleUse use)
    {
        if (use == HandleUse.Reset)
        {
            easy!.Reset();
        }

        if (use != HandleUse.Fresh)
        {
            return easy;
        }

        if (easy is not null)
        {
            await easy.DisposeAsync().ConfigureAwait(false);
        }

        return steps.EasyInitOrMajorBad(library, out CurlEasy? fresh) ? fresh : null;
    }

    private static (CurlOption Option, object? Value)[] Download(LibTestContext context, string url, FtpFileMethod method) =>
    [
        (CurlOption.Writedata, context.StandardOutput),
        (CurlOption.Url, url),
        (CurlOption.Verbose, 1L),
        (CurlOption.IgnoreContentLength, 1L),
        (CurlOption.FtpFilemethod, (long)method),
    ];

    private static (CurlOption Option, object? Value)[] Home(LibTestContext context, FtpFileMethod method, CurlSlist quote) =>
    [
        (CurlOption.Writedata, context.StandardOutput),
        (CurlOption.Url, context.Url),
        (CurlOption.Verbose, 1L),
        (CurlOption.Nobody, 1L),
        (CurlOption.FtpFilemethod, (long)method),
        (CurlOption.Quote, quote),
    ];
}
