namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// The wildcard hooks libcurl behaves as when no chunk or match callback is set: names are
/// matched by <see cref="FtpWildcardPattern.Match" /> as the platform's curl matches them (curl's
/// own <c>Curl_fnmatch</c> on Windows, the system <c>fnmatch</c> on Linux and macOS), and every
/// match is downloaded.
/// </summary>
/// <param name="matcher">Which platform's <c>Curl_fnmatch</c> names are matched as.</param>
public sealed class FtpDownloadEveryMatch(FtpWildcardMatcher matcher) : IFtpWildcardHooks
{
    /// <summary>Gets the hooks for the platform Curl runs on; they hold no state.</summary>
    public static FtpDownloadEveryMatch Instance { get; } =
        new(FtpWildcardPattern.MatcherFor(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()));

    /// <inheritdoc />
    public FtpNameMatchAnswer MatchName(string pattern, string fileName) =>
        FtpWildcardPattern.Match(pattern, fileName, matcher);

    /// <inheritdoc />
    public FtpChunkBeginAnswer BeginChunk(FtpFileListEntry entry, int remaining) => FtpChunkBeginAnswer.Transfer;

    /// <inheritdoc />
    public FtpChunkEndAnswer EndChunk() => FtpChunkEndAnswer.CarryOn;
}
