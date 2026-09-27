using System.Globalization;

namespace Curl.Core.Globbing;

/// <summary>
/// One URL a <see cref="UrlGlob" /> expanded to, with the value each of its globs took for
/// it, which <c>#1</c>, <c>#2</c>, ... in an <c>-o</c> file name stand for.
/// </summary>
public sealed class UrlGlobMatch
{
    internal UrlGlobMatch(string url, IReadOnlyList<string> globValues)
    {
        Url = url;
        GlobValues = globValues;
    }

    /// <summary>Gets the expanded URL.</summary>
    public string Url { get; }

    /// <summary>Gets the value each glob took, the first glob's first; empty under <c>-g</c>.</summary>
    public IReadOnlyList<string> GlobValues { get; }

    /// <summary>
    /// Replaces each <c>#N</c> in <paramref name="outputFileName" /> with the value glob N
    /// took, as curl 8.21.0's <c>glob_match_url</c> does.
    /// </summary>
    /// <remarks>
    /// <c>N</c> is the whole run of digits after the <c>#</c>, so <c>#01</c> is glob 1. A run
    /// that names no glob - <c>#0</c>, a number past the last glob, or one too long to read -
    /// leaves the <c>#</c> and its digits as written, as does a <c>#</c> with no digit after it.
    /// </remarks>
    /// <param name="outputFileName">The <c>-o</c> file name as given.</param>
    /// <returns>The file name with every <c>#N</c> that names a glob replaced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outputFileName" /> is <see langword="null" />.</exception>
    public string SubstituteGlobValues(string outputFileName)
    {
        ArgumentNullException.ThrowIfNull(outputFileName);

        var result = new System.Text.StringBuilder(outputFileName.Length);
        int index = 0;
        while (index < outputFileName.Length)
        {
            index = AppendNext(outputFileName, index, result);
        }

        return result.ToString();
    }

    /// <summary>
    /// Appends the glob value a <c>#N</c> at <paramref name="index" /> names, or else the one
    /// character there, and returns the index after what it consumed.
    /// </summary>
    private int AppendNext(string outputFileName, int index, System.Text.StringBuilder result)
    {
        ReadOnlySpan<char> afterHash = outputFileName.AsSpan(index + 1);
        int digitCount = afterHash.IndexOfAnyExceptInRange('0', '9');
        if (digitCount < 0)
        {
            digitCount = afterHash.Length;
        }

        if (outputFileName[index] == '#'
            && long.TryParse(afterHash[..digitCount], NumberStyles.None, CultureInfo.InvariantCulture, out long globNumber)
            && globNumber >= 1
            && globNumber <= GlobValues.Count)
        {
            result.Append(GlobValues[(int)globNumber - 1]);
            return index + 1 + digitCount;
        }

        result.Append(outputFileName[index]);
        return index + 1;
    }
}
