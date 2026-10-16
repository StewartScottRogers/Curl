namespace Curl.Protocol.Ftp;

/// <summary>
/// The wildcard hooks libcurl behaves as when no chunk or match callback is set: names are
/// matched by <see cref="FtpWildcardPattern.Matches" />, curl's own <c>Curl_fnmatch</c>, and
/// every match is downloaded.
/// </summary>
public sealed class FtpDownloadEveryMatch : IFtpWildcardHooks
{
    private FtpDownloadEveryMatch()
    {
    }

    /// <summary>Gets the one instance; it holds no state.</summary>
    public static FtpDownloadEveryMatch Instance { get; } = new();

    /// <inheritdoc />
    public FtpNameMatchAnswer MatchName(string pattern, string fileName) =>
        FtpWildcardPattern.Matches(pattern, fileName) ? FtpNameMatchAnswer.Match : FtpNameMatchAnswer.NoMatch;

    /// <inheritdoc />
    public FtpChunkBeginAnswer BeginChunk(FtpFileListEntry entry, int remaining) => FtpChunkBeginAnswer.Transfer;

    /// <inheritdoc />
    public FtpChunkEndAnswer EndChunk() => FtpChunkEndAnswer.CarryOn;
}
