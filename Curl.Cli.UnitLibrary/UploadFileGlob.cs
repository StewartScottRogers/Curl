using System.Diagnostics.CodeAnalysis;
using Curl.Core.Globbing;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// A <c>-T</c> / <c>--upload-file</c> argument as curl 8.21.0 globs it: the <c>{a,b}</c> sets and
/// <c>[1-3]</c> ranges in it expand, with the engine URLs use (<see cref="UrlGlob" />), into one
/// upload file per match, and each file is sent to the URL <see cref="UploadTransferUrl" />
/// resolves for it. Under <c>-g</c>/<c>--globoff</c> the argument is one file name, taken as
/// written. Nothing here reads the file system.
/// </summary>
/// <remarks>
/// Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27:
/// <c>-T '{local.txt,sub/in.txt}' http://h/g/</c> uploads <c>local.txt</c> to
/// <c>http://h/g/local.txt</c> and then <c>sub/in.txt</c> to <c>http://h/g/in.txt</c>; the upload
/// files are the outer loop, so each file goes to every URL a URL glob expands to before the next
/// file starts; a <c>-</c> among the matches still means standard input; a malformed glob is exit
/// 3 with the URL glob's <c>&lt;reason&gt; in position N:</c> message about the <c>-T</c> text.
/// </remarks>
public sealed class UploadFileGlob
{
    private readonly UrlGlob glob;

    private UploadFileGlob(UrlGlob glob) => this.glob = glob;

    /// <summary>Gets how many upload files <see cref="ExpandUploadFiles" /> produces; always at least one.</summary>
    public long UploadFileCount => glob.UrlCount;

    /// <summary>Parses the <c>-T</c> argument <paramref name="uploadFile" /> as a glob.</summary>
    /// <param name="uploadFile">The <c>-T</c> argument as given.</param>
    /// <param name="globOff">
    /// <see langword="true" /> under <c>-g</c>/<c>--globoff</c>: the argument is one file name,
    /// braces and brackets included.
    /// </param>
    /// <param name="glob">The glob; <see langword="null" /> when parsing failed.</param>
    /// <param name="failure">
    /// The exit 3 failure with curl's message; <see langword="null" /> when parsing succeeded.
    /// </param>
    /// <returns><see langword="true" /> when <paramref name="glob" /> was produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uploadFile" /> is <see langword="null" />.</exception>
    public static bool TryParse(
        string uploadFile,
        bool globOff,
        [NotNullWhen(true)] out UploadFileGlob? glob,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        if (globOff)
        {
            glob = new UploadFileGlob(UrlGlob.Unglobbed(uploadFile));
            failure = null;
            return true;
        }

        if (!UrlGlob.TryParse(uploadFile, out UrlGlob? parsed, out failure))
        {
            glob = null;
            return false;
        }

        glob = new UploadFileGlob(parsed);
        return true;
    }

    /// <summary>Produces every upload file name the argument stands for, in curl's order.</summary>
    /// <returns>The file names, rightmost glob fastest.</returns>
    public IEnumerable<string> ExpandUploadFiles()
    {
        foreach (UrlGlobMatch match in ExpandUploadMatches())
        {
            yield return match.Url;
        }
    }

    /// <summary>
    /// Produces every upload file the argument stands for, in curl's order, with the value each
    /// of its globs took, which a <c>#&lt;name&gt;</c> in an <c>-o</c> name can refer to
    /// (<see cref="UrlGlobMatch.TryResolveOutputFileName(string, UrlGlobMatch?, bool, out string?, out TransferResult?)" />).
    /// </summary>
    /// <returns>The matches, rightmost glob fastest; each match's <see cref="UrlGlobMatch.Url" /> is the file name.</returns>
    public IEnumerable<UrlGlobMatch> ExpandUploadMatches() => glob.Expand();

    /// <summary>
    /// Produces, for each upload file in curl's order, the URL it is sent to when the URL given for
    /// it is <paramref name="url" />, as <see cref="UploadTransferUrl.TryResolve" /> resolves it.
    /// </summary>
    /// <param name="url">The URL paired with this <c>-T</c> argument, as typed.</param>
    /// <returns>One target per upload file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public IEnumerable<UploadTransferTarget> ResolveTransferTargets(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        foreach (string uploadFile in ExpandUploadFiles())
        {
            bool isUrlWellFormed = UploadTransferUrl.TryResolve(url, uploadFile, out string transferUrl);
            yield return new UploadTransferTarget(uploadFile, transferUrl, isUrlWellFormed);
        }
    }
}
