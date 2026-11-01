namespace Curl.Original.Protocol.Ftp.Fakes;

/// <summary>
/// Wildcard hooks that answer as a test sets them and record every call, in order, as
/// <c>match &lt;name&gt;</c>, <c>begin &lt;name&gt; &lt;remaining&gt;</c> and <c>end</c>.
/// </summary>
public sealed class RecordingWildcardHooks : IFtpWildcardHooks
{
    /// <summary>
    /// Gets what every name match answers; <see langword="null" />, the default, matches as
    /// <see cref="FtpWildcardPattern.Matches" /> does.
    /// </summary>
    public FtpNameMatchAnswer? MatchAnswer { get; init; }

    /// <summary>Gets what the chunk-begin hook answers for an entry; download, by default.</summary>
    public Func<FtpFileListEntry, FtpChunkBeginAnswer> BeginAnswer { get; init; } = _ => FtpChunkBeginAnswer.Transfer;

    /// <summary>Gets what the chunk-end hook answers.</summary>
    public FtpChunkEndAnswer EndAnswer { get; init; }

    /// <summary>Gets every call made, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc />
    public FtpNameMatchAnswer MatchName(string pattern, string fileName)
    {
        Calls.Add("match " + fileName);
        return MatchAnswer ?? (FtpWildcardPattern.Matches(pattern, fileName) ? FtpNameMatchAnswer.Match : FtpNameMatchAnswer.NoMatch);
    }

    /// <inheritdoc />
    public FtpChunkBeginAnswer BeginChunk(FtpFileListEntry entry, int remaining)
    {
        Calls.Add($"begin {entry.FileName} {remaining}");
        return BeginAnswer(entry);
    }

    /// <inheritdoc />
    public FtpChunkEndAnswer EndChunk()
    {
        Calls.Add("end");
        return EndAnswer;
    }
}
