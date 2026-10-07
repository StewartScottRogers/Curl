using System.Diagnostics.CodeAnalysis;
using Curl.Cli;
using Curl.Core.Globbing;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// One transfer the command line asks for: one URL a command-line URL's glob expanded to, the
/// position of that command-line URL, the run-wide number of the transfer, and the output its
/// body goes to.
/// </summary>
/// <remarks>
/// Every URL a glob expands to shares its command-line URL's <c>-o</c> or remote-name entry,
/// its <c>-T</c> argument and its <c>%{urlnum}</c>, while each takes the next <c>%{xfer_id}</c>, as
/// curl 8.21.0 does: <c>-o 'o#1' file:///{a,b}.txt file:///a.txt -o last</c> printed urlnum
/// <c>0 0 1</c>, xfer_id <c>0 1 2</c> and wrote <c>oa</c>, <c>ob</c> and <c>last</c> (measured
/// 2026-09-27, BL-240 Notes).
/// </remarks>
internal sealed class UrlTransfer
{
    /// <summary>
    /// The <c>-o</c> value that sends the body to standard output rather than a file, with or
    /// without <c>--output-dir</c>, as curl 8.21.0 does: <c>--output-dir d -o -</c> wrote the body
    /// to standard output, created no <c>d</c> and left <c>%{filename_effective}</c> empty
    /// (measured 2026-09-27, BL-349 Notes).
    /// </summary>
    internal const string StandardOutputFileName = "-";

    /// <summary>
    /// Creates the transfer of one URL <paramref name="match" /> names, or curl 8.21.0's exit 43
    /// failure when its <c>-o</c> name holds a <c>#&lt;name&gt;</c> naming no glob of the URL or of
    /// the <c>-T</c> upload glob (<see cref="UrlGlobMatch.TryResolveOutputFileName(string, UrlGlobMatch?, bool, out string?, out TransferResult?)" />;
    /// upstream test2411).
    /// </summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="urlIndex">The position of the command-line URL the glob came from, in its option group.</param>
    /// <param name="urlNumber">The run-wide zero-based number of that command-line URL.</param>
    /// <param name="transferId">The run-wide zero-based number of this transfer.</param>
    /// <param name="match">The URL the glob expanded to, with its glob values.</param>
    /// <param name="uploadMatch">
    /// The <c>-T</c> file this transfer uploads, one match of the <c>-T</c> glob with its glob values;
    /// <see langword="null" /> for no upload.
    /// </param>
    /// <param name="sanitizesForWindows">Whether the <c>-o</c> name is sanitized as curl's Windows build does.</param>
    /// <param name="transfer">The transfer; <see langword="null" /> on failure.</param>
    /// <param name="failure">The exit 43 failure; <see langword="null" /> on success.</param>
    /// <returns><see langword="true" /> when <paramref name="transfer" /> was produced.</returns>
    internal static bool TryCreate(
        CommandLineOptions options,
        int urlIndex,
        int urlNumber,
        long transferId,
        UrlGlobMatch match,
        UrlGlobMatch? uploadMatch,
        bool sanitizesForWindows,
        [NotNullWhen(true)] out UrlTransfer? transfer,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        string? outputFileName = null;
        failure = null;
        if (options.UrlOutputs[urlIndex].FileName is { } fileName and not StandardOutputFileName
            && !match.TryResolveOutputFileName(fileName, uploadMatch, sanitizesForWindows, out outputFileName, out failure))
        {
            transfer = null;
            return false;
        }

        transfer = new UrlTransfer(options, urlIndex, urlNumber, transferId, match.Url, uploadMatch?.Url, outputFileName);
        return true;
    }

    /// <summary>Creates the transfer of one URL a glob expanded to, its <c>-o</c> name already resolved.</summary>
    private UrlTransfer(
        CommandLineOptions options,
        int urlIndex,
        int urlNumber,
        long transferId,
        string url,
        string? uploadFile,
        string? outputFileName)
    {
        // The parser gives every URL an output entry at its own position.
        UrlOutput output = options.UrlOutputs[urlIndex];
        UrlIndex = urlIndex;
        UrlNumber = urlNumber;
        TransferId = transferId;
        Url = url;
        UploadFile = uploadFile;
        OutputFileName = outputFileName;
        UsesRemoteName = output.UsesRemoteName;
        DiscardsBody = output.DiscardsBody;
    }

    /// <summary>Gets the position of the command-line URL in its option group, which pairs it with its output entry.</summary>
    internal int UrlIndex { get; }

    /// <summary>Gets the run-wide number of the command-line URL, printed by <c>%{urlnum}</c>.</summary>
    internal int UrlNumber { get; }

    /// <summary>Gets the run-wide zero-based number of the transfer, printed by <c>%{xfer_id}</c>.</summary>
    internal long TransferId { get; }

    /// <summary>Gets the URL as the glob expanded it, before any scheme is guessed or IPFS gateway applied.</summary>
    internal string Url { get; }

    /// <summary>
    /// Gets the <c>-T</c> file this transfer uploads, one match of its command-line URL's <c>-T</c> glob;
    /// <see langword="null" /> for no upload.
    /// </summary>
    internal string? UploadFile { get; }

    /// <summary>
    /// Gets the <c>-o</c> file name with each <c>#N</c> substituted and, on Windows, sanitized;
    /// <see langword="null" /> without <c>-o</c> or with <c>-o -</c> (<see cref="StandardOutputFileName" />).
    /// </summary>
    internal string? OutputFileName { get; }

    /// <summary>Gets whether the body is saved under the remote name, when there is no <c>-o</c> name.</summary>
    internal bool UsesRemoteName { get; }

    /// <summary>
    /// Gets whether <c>--out-null</c> throws the body away: no file, nothing on standard output.
    /// </summary>
    internal bool DiscardsBody { get; }

    /// <summary>Gets whether the body goes to a file: an <c>-o</c> name or the remote name.</summary>
    internal bool WritesToFile => OutputFileName is not null || UsesRemoteName;
}
