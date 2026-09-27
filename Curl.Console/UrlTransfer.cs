using Curl.Cli;
using Curl.Core.Globbing;

namespace Curl.Console;

/// <summary>
/// One transfer the command line asks for: one URL a command-line URL's glob expanded to, the
/// position of that command-line URL, the run-wide number of the transfer, and the output its
/// body goes to.
/// </summary>
/// <remarks>
/// Every URL a glob expands to shares its command-line URL's <c>-o</c> or remote-name entry,
/// its <c>-T</c> file and its <c>%{urlnum}</c>, while each takes the next <c>%{xfer_id}</c>, as
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

    /// <summary>Creates the transfer of one URL <paramref name="match" /> names.</summary>
    /// <param name="options">The accepted command line.</param>
    /// <param name="urlIndex">The position of the command-line URL the glob came from.</param>
    /// <param name="transferId">The run-wide zero-based number of this transfer.</param>
    /// <param name="match">The URL the glob expanded to, with its glob values.</param>
    /// <param name="sanitizesForWindows">
    /// Whether the <c>-o</c> name is sanitized as curl's Windows build does, after each
    /// <c>#N</c> is substituted (<see cref="UrlGlobMatch.ResolveOutputFileName" />).
    /// </param>
    internal UrlTransfer(CommandLineOptions options, int urlIndex, long transferId, UrlGlobMatch match, bool sanitizesForWindows)
    {
        UrlOutput? output = urlIndex < options.UrlOutputs.Count ? options.UrlOutputs[urlIndex] : null;
        UrlIndex = urlIndex;
        TransferId = transferId;
        Url = match.Url;
        OutputFileName = output?.FileName is { } fileName and not StandardOutputFileName
            ? match.ResolveOutputFileName(fileName, sanitizesForWindows)
            : null;
        UsesRemoteName = output?.UsesRemoteName ?? false;
    }

    /// <summary>Gets the position of the command-line URL, printed by <c>%{urlnum}</c>.</summary>
    internal int UrlIndex { get; }

    /// <summary>Gets the run-wide zero-based number of the transfer, printed by <c>%{xfer_id}</c>.</summary>
    internal long TransferId { get; }

    /// <summary>Gets the URL as the glob expanded it, before any scheme is guessed or IPFS gateway applied.</summary>
    internal string Url { get; }

    /// <summary>
    /// Gets the <c>-o</c> file name with each <c>#N</c> substituted and, on Windows, sanitized;
    /// <see langword="null" /> without <c>-o</c> or with <c>-o -</c> (<see cref="StandardOutputFileName" />).
    /// </summary>
    internal string? OutputFileName { get; }

    /// <summary>Gets whether the body is saved under the remote name, when there is no <c>-o</c> name.</summary>
    internal bool UsesRemoteName { get; }

    /// <summary>Gets whether the body goes to a file: an <c>-o</c> name or the remote name.</summary>
    internal bool WritesToFile => OutputFileName is not null || UsesRemoteName;
}
