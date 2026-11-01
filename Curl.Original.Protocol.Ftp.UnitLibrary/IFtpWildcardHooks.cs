namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// The three calls a wildcard download makes, as upstream curl 8.22.0 calls libcurl's
/// <c>CURLOPT_FNMATCH_FUNCTION</c>, <c>CURLOPT_CHUNK_BGN_FUNCTION</c> and
/// <c>CURLOPT_CHUNK_END_FUNCTION</c> (ADR-0615). Setting
/// <see cref="FtpProtocolHandler.WildcardHooks" /> turns wildcard matching on;
/// <see cref="FtpDownloadEveryMatch" /> is the set libcurl uses when none of the three is set.
/// </summary>
public interface IFtpWildcardHooks
{
    /// <summary>
    /// Tells whether a file of the directory's <c>LIST</c> reply is one of the matches; called
    /// once for every entry, in the reply's order, while the listing arrives.
    /// </summary>
    /// <param name="pattern">The URL path's last part, still percent-encoded, as curl passes it.</param>
    /// <param name="fileName">The entry's file name.</param>
    /// <returns>
    /// <see cref="FtpNameMatchAnswer.Match" /> to keep the entry. Any other answer, a failure
    /// included, leaves it out, as curl's <c>ftp_pl_insert_finfo</c> keeps only a 0 answer.
    /// </returns>
    FtpNameMatchAnswer MatchName(string pattern, string fileName);

    /// <summary>Called before each match is downloaded, a non-file match included.</summary>
    /// <param name="entry">The match.</param>
    /// <param name="remaining">The matches not yet handled, this one included.</param>
    /// <returns>
    /// Whether to download the match (a non-file is skipped all the same), skip it, or end the
    /// whole download with exit 88 and no <c>QUIT</c>.
    /// </returns>
    FtpChunkBeginAnswer BeginChunk(FtpFileListEntry entry, int remaining);

    /// <summary>
    /// Called once each match is done with: after its download, failed or not, or after it was
    /// skipped; never after a failing <see cref="BeginChunk" />.
    /// </summary>
    /// <returns>
    /// The hook's answer, which the download ignores, as curl 8.22.0's <c>ftp_done</c> and
    /// <c>wc_statemach</c> ignore the chunk-end callback's return value (ADR-0615).
    /// </returns>
    FtpChunkEndAnswer EndChunk();
}
