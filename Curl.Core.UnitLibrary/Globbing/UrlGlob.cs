using System.Diagnostics.CodeAnalysis;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Globbing;

/// <summary>
/// A URL as curl 8.21.0's command-line tool globs it: the sets (<c>{a,b}</c>) and ranges
/// (<c>[1-10]</c>, <c>[01-10]</c>, <c>[a-z:2]</c>) it holds, and the URLs they expand to in
/// curl's order, each with the values <c>#N</c> in an <c>-o</c> file name stands for.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TryParse" /> reads the URL as <c>tool_urlglob.c</c> does; text that is not a
/// well-formed glob is exit 3, <see cref="CurlExitCode.UrlMalformat" />, with curl's
/// <c>&lt;reason&gt; in position N:</c> message, the URL, and a caret under column N.
/// <see cref="Unglobbed" /> is the URL under <c>-g</c>/<c>--globoff</c>: exactly one URL,
/// taken as written, and every <c>#N</c> left as written.
/// </para>
/// <para>
/// Expansion runs the rightmost glob fastest, so <c>{a,b}[1-2]</c> is <c>a1</c>, <c>a2</c>,
/// <c>b1</c>, <c>b2</c>. URLs are produced one at a time, so a glob of billions of URLs
/// costs nothing until it is walked. Every rule here was measured against curl 8.21.0 on
/// 2026-09-26 over <c>file://</c>.
/// </para>
/// </remarks>
public sealed class UrlGlob
{
    private readonly IReadOnlyList<UrlGlobPiece> pieces;
    private readonly bool isGlobbing;

    private UrlGlob(IReadOnlyList<UrlGlobPiece> pieces, long urlCount, bool isGlobbing)
    {
        this.pieces = pieces;
        UrlCount = urlCount;
        this.isGlobbing = isGlobbing;
    }

    /// <summary>Gets how many URLs <see cref="Expand" /> produces; always at least one.</summary>
    public long UrlCount { get; }

    /// <summary>Parses <paramref name="url" /> as a URL glob.</summary>
    /// <param name="url">The URL as given on the command line.</param>
    /// <param name="glob">The glob; <see langword="null" /> when parsing failed.</param>
    /// <param name="failure">
    /// The exit 3 failure with curl's message; <see langword="null" /> when parsing succeeded.
    /// </param>
    /// <returns><see langword="true" /> when <paramref name="glob" /> was produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static bool TryParse(
        string url,
        [NotNullWhen(true)] out UrlGlob? glob,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        ArgumentNullException.ThrowIfNull(url);

        var parser = new UrlGlobParser(url);
        if (!parser.TryParse(out UrlGlobError? error))
        {
            glob = null;
            failure = TransferResult.Failure(CurlExitCode.UrlMalformat, error.ToMessage(url));
            return false;
        }

        glob = new UrlGlob(parser.Pieces, parser.UrlCount, isGlobbing: true);
        failure = null;
        return true;
    }

    /// <summary>
    /// Gets the URL as <c>-g</c>/<c>--globoff</c> takes it: one URL, exactly as written,
    /// with no glob values, so every <c>#N</c> in an output file name stays as written.
    /// </summary>
    /// <param name="url">The URL as given on the command line.</param>
    /// <returns>A glob that expands to <paramref name="url" /> alone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url" /> is <see langword="null" />.</exception>
    public static UrlGlob Unglobbed(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new UrlGlob([UrlGlobPiece.Fixed(url)], 1, isGlobbing: false);
    }

    /// <summary>Produces every URL the glob stands for, rightmost glob fastest.</summary>
    /// <returns>The URLs, each with its glob values.</returns>
    public IEnumerable<UrlGlobMatch> Expand()
    {
        var indexes = new long[pieces.Count];
        for (long produced = 0; produced < UrlCount; produced++)
        {
            yield return CurrentMatch(indexes);
            Advance(indexes);
        }
    }

    private UrlGlobMatch CurrentMatch(long[] indexes)
    {
        var url = new System.Text.StringBuilder();
        var globValues = new List<string>();
        var globNames = new List<string?>();
        for (int position = 0; position < pieces.Count; position++)
        {
            string value = pieces[position].ValueAt(indexes[position]);
            url.Append(value);
            if (pieces[position].IsGlob)
            {
                globValues.Add(value);
                globNames.Add(pieces[position].Name);
            }
        }

        return new UrlGlobMatch(url.ToString(), globValues, globNames, isGlobbing);
    }

    private void Advance(long[] indexes)
    {
        for (int position = pieces.Count - 1; position >= 0; position--)
        {
            if (++indexes[position] < pieces[position].Count)
            {
                return;
            }

            indexes[position] = 0;
        }
    }
}
