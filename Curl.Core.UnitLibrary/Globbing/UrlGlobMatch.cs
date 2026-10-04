using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Globbing;

/// <summary>
/// One URL a <see cref="UrlGlob" /> expanded to, with the value each of its globs took for
/// it, which <c>#1</c>, <c>#2</c>, ... and <c>#&lt;name&gt;</c> in an <c>-o</c> file name
/// stand for.
/// </summary>
public sealed class UrlGlobMatch
{
    private readonly List<string?> globNames;
    private readonly bool isGlobbing;

    internal UrlGlobMatch(string url, IReadOnlyList<string> globValues, List<string?> globNames, bool isGlobbing)
    {
        Url = url;
        GlobValues = globValues;
        this.globNames = globNames;
        this.isGlobbing = isGlobbing;
    }

    /// <summary>Gets the expanded URL.</summary>
    public string Url { get; }

    /// <summary>Gets the value each glob took, the first glob's first; empty under <c>-g</c>.</summary>
    public IReadOnlyList<string> GlobValues { get; }

    /// <summary>
    /// Replaces each <c>#N</c> and <c>#&lt;name&gt;</c> in <paramref name="outputFileName" />
    /// with the value that glob took, as curl 8.21.0's <c>glob_match_url</c> does.
    /// </summary>
    /// <remarks>
    /// <c>N</c> is the whole run of digits after the <c>#</c>, so <c>#01</c> is glob 1; it
    /// counts every glob, named or not. A run that names no glob - <c>#0</c>, a number past
    /// the last glob, or one too long to read - leaves the <c>#</c> and its digits as written,
    /// as does a <c>#</c> with no digit after it, and a malformed <c>#&lt;</c> with no
    /// <c>&gt;</c> or a name over 64 characters. A well-formed <c>#&lt;name&gt;</c> naming no
    /// glob, which curl refuses, is also left as written here;
    /// <see cref="TryResolveOutputFileName" /> reports it as curl does.
    /// </remarks>
    /// <param name="outputFileName">The <c>-o</c> file name as given.</param>
    /// <returns>The file name with every reference that names a glob replaced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outputFileName" /> is <see langword="null" />.</exception>
    public string SubstituteGlobValues(string outputFileName)
    {
        ArgumentNullException.ThrowIfNull(outputFileName);
        return Substitute(outputFileName, unknownNameFails: false, out _);
    }

    /// <summary>
    /// Gets the file name curl 8.21.0 writes this URL to for the <c>-o</c> name
    /// <paramref name="outputFileName" />.
    /// </summary>
    /// <remarks>
    /// Under <c>-g</c>/<c>--globoff</c> curl never runs <c>glob_match_url</c>, so the name is
    /// used as written. Otherwise, even for a URL with no glob in it, each <c>#N</c> and
    /// <c>#&lt;name&gt;</c> is substituted (<see cref="SubstituteGlobValues" />) and, on
    /// Windows, the whole result is then sanitized as curl's <c>sanitize_file_name</c> does:
    /// control characters and <c>| &lt; &gt; " ? *</c> become <c>_</c>, while path
    /// separators, colons and reserved device names stay.
    /// </remarks>
    /// <param name="outputFileName">The <c>-o</c> file name as given.</param>
    /// <param name="sanitizesForWindows">
    /// <see langword="true" /> to sanitize as curl's Windows build does; pass
    /// <see cref="OperatingSystem.IsWindows" />.
    /// </param>
    /// <returns>The file name curl writes to.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outputFileName" /> is <see langword="null" />.</exception>
    public string ResolveOutputFileName(string outputFileName, bool sanitizesForWindows)
    {
        ArgumentNullException.ThrowIfNull(outputFileName);

        if (!isGlobbing)
        {
            return outputFileName;
        }

        string substituted = SubstituteGlobValues(outputFileName);
        return sanitizesForWindows ? WindowsOutputFileNameSanitizer.Sanitize(substituted) : substituted;
    }

    /// <summary>
    /// Gets the file name curl 8.21.0 writes this URL to, as
    /// <see cref="ResolveOutputFileName" /> does, or curl's failure when the name holds a
    /// well-formed <c>#&lt;name&gt;</c> naming no glob of this URL.
    /// </summary>
    /// <remarks>
    /// That failure is exit 43, <see cref="CurlExitCode.BadFunctionArgument" />, with curl's
    /// <c>no glob exists with this name in position N:</c> message, the file name, and a
    /// caret under column N, where N is the offset just past the reference's <c>&gt;</c>.
    /// Names are looked up in this URL's globs only, not in a <c>-T</c> upload glob.
    /// </remarks>
    /// <param name="outputFileName">The <c>-o</c> file name as given.</param>
    /// <param name="sanitizesForWindows">
    /// <see langword="true" /> to sanitize as curl's Windows build does; pass
    /// <see cref="OperatingSystem.IsWindows" />.
    /// </param>
    /// <param name="fileName">The file name curl writes to; <see langword="null" /> on failure.</param>
    /// <param name="failure">The exit 43 failure; <see langword="null" /> on success.</param>
    /// <returns><see langword="true" /> when <paramref name="fileName" /> was produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outputFileName" /> is <see langword="null" />.</exception>
    public bool TryResolveOutputFileName(
        string outputFileName,
        bool sanitizesForWindows,
        [NotNullWhen(true)] out string? fileName,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        ArgumentNullException.ThrowIfNull(outputFileName);

        failure = null;
        if (!isGlobbing)
        {
            fileName = outputFileName;
            return true;
        }

        string substituted = Substitute(outputFileName, unknownNameFails: true, out UrlGlobError? error);
        if (error is not null)
        {
            fileName = null;
            failure = TransferResult.Failure(CurlExitCode.BadFunctionArgument, error.ToMessage(outputFileName));
            return false;
        }

        fileName = sanitizesForWindows ? WindowsOutputFileNameSanitizer.Sanitize(substituted) : substituted;
        return true;
    }

    private string Substitute(string outputFileName, bool unknownNameFails, out UrlGlobError? error)
    {
        error = null;
        var result = new System.Text.StringBuilder(outputFileName.Length);
        int index = 0;
        while (index < outputFileName.Length && error is null)
        {
            index = AppendNext(outputFileName, index, result, unknownNameFails, ref error);
        }

        return result.ToString();
    }

    /// <summary>
    /// Appends the glob value a <c>#N</c> or <c>#&lt;name&gt;</c> at <paramref name="index" />
    /// names, or else the one character there, and returns the index after what it consumed.
    /// </summary>
    private int AppendNext(
        string outputFileName,
        int index,
        System.Text.StringBuilder result,
        bool unknownNameFails,
        ref UrlGlobError? error)
    {
        if (outputFileName[index] == '#')
        {
            string? name = UrlGlobName.Read(outputFileName, index + 1);
            int end = name is null
                ? AppendNumberedValue(outputFileName, index, result)
                : AppendNamedValue(name, index, result);
            if (end >= 0)
            {
                return end;
            }

            if (unknownNameFails && name is not null)
            {
                error = new UrlGlobError("no glob exists with this name", index + name.Length + 3);
                return index;
            }
        }

        result.Append(outputFileName[index]);
        return index + 1;
    }

    /// <summary>
    /// Appends the value of the glob named <paramref name="name" /> and returns the index past
    /// the <c>#&lt;name&gt;</c> at <paramref name="index" />, or returns -1 when no glob has
    /// that name.
    /// </summary>
    private int AppendNamedValue(string name, int index, System.Text.StringBuilder result)
    {
        int globIndex = globNames.IndexOf(name);
        if (globIndex < 0)
        {
            return -1;
        }

        result.Append(GlobValues[globIndex]);
        return index + name.Length + 3;
    }

    /// <summary>
    /// Appends the value of the glob a <c>#N</c> at <paramref name="index" /> names and
    /// returns the index past it, or returns -1 when it names no glob.
    /// </summary>
    private int AppendNumberedValue(string outputFileName, int index, System.Text.StringBuilder result)
    {
        ReadOnlySpan<char> afterHash = outputFileName.AsSpan(index + 1);
        int digitCount = afterHash.IndexOfAnyExceptInRange('0', '9');
        if (digitCount < 0)
        {
            digitCount = afterHash.Length;
        }

        if (long.TryParse(afterHash[..digitCount], NumberStyles.None, CultureInfo.InvariantCulture, out long globNumber)
            && globNumber >= 1
            && globNumber <= GlobValues.Count)
        {
            result.Append(GlobValues[(int)globNumber - 1]);
            return index + 1 + digitCount;
        }

        return -1;
    }
}
