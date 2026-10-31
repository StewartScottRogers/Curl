using Curl.Api.Callbacks;
using Curl.Api.Options;
using Curl.Original.Protocol.Ftp;

namespace Curl.Engine;

/// <summary>
/// The FTP handler's wildcard hooks over a handle's <c>CURLOPT_FNMATCH_FUNCTION</c>, <c>CURLOPT_CHUNK_BGN_FUNCTION</c>
/// and <c>CURLOPT_CHUNK_END_FUNCTION</c> (ADR-0619), as curl 8.22.0's <c>lib/ftp.c</c> and <c>lib/ftplistparser.c</c>
/// call them: each hook not set behaves as libcurl's default (<see cref="FtpDownloadEveryMatch" />: the platform's
/// <c>Curl_fnmatch</c>, every match downloaded). The chunk-begin callback is given each match as a
/// <see cref="CurlFileInfo" /> (<see cref="FileInfoOf" />); any answer but <see cref="ChunkBeginResult.Skip" /> or
/// <see cref="ChunkBeginResult.Fail" /> downloads it, as <c>wc_statemach</c>'s <c>switch</c> does.
/// </summary>
/// <param name="fileNameMatch">The handle's match callback, or <see langword="null" /> for libcurl's own.</param>
/// <param name="chunkBegin">The handle's chunk-begin callback, or <see langword="null" />.</param>
/// <param name="chunkEnd">The handle's chunk-end callback, or <see langword="null" />.</param>
/// <param name="defaults">The hooks a callback not set falls back to.</param>
internal sealed class CallbackWildcardHooks(FileNameMatchCallback? fileNameMatch, ChunkBeginCallback? chunkBegin, ChunkEndCallback? chunkEnd, IFtpWildcardHooks defaults) : IFtpWildcardHooks
{
    /// <summary>
    /// Gives the wildcard hooks a transfer's FTP handler runs with: none without <c>CURLOPT_WILDCARDMATCH</c>, so a
    /// wildcard URL is fetched as a plain path, else the handle's callbacks over libcurl's defaults.
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    /// <returns>The hooks, or <see langword="null" /> with wildcard matching off.</returns>
    internal static IFtpWildcardHooks? Of(EasyOptions options) =>
        options.Behaviour.WildcardMatch
            ? new CallbackWildcardHooks(options.Callbacks.FileNameMatch, options.Callbacks.ChunkBegin, options.Callbacks.ChunkEnd, FtpDownloadEveryMatch.Instance)
            : null;

    /// <summary>
    /// Maps one entry of the listing to libcurl's <c>struct curl_fileinfo</c>: the fields <c>ftplistparser.c</c>
    /// fills, the type by its <c>curlfiletype</c> number. The parser never fills <c>time</c>, <c>uid</c> or
    /// <c>gid</c>, so they stay <see langword="null" />.
    /// </summary>
    /// <param name="entry">The listing's entry.</param>
    /// <returns>The file information.</returns>
    internal static CurlFileInfo FileInfoOf(FtpFileListEntry entry) => new()
    {
        FileName = entry.FileName,
        FileType = (CurlFileType)(int)entry.FileType,
        Permissions = entry.Permissions,
        Size = entry.Size,
        HardLinks = entry.HardLinks,
        TimeText = entry.TimeText,
        PermissionsText = entry.PermissionsText,
        User = entry.User,
        Group = entry.Group,
        Target = entry.Target,
    };

    /// <inheritdoc />
    public FtpNameMatchAnswer MatchName(string pattern, string fileName) =>
        fileNameMatch is null ? defaults.MatchName(pattern, fileName) : (FtpNameMatchAnswer)(int)fileNameMatch(pattern, fileName);

    /// <inheritdoc />
    public FtpChunkBeginAnswer BeginChunk(FtpFileListEntry entry, int remaining) =>
        chunkBegin is null ? defaults.BeginChunk(entry, remaining) : BeginAnswerOf(chunkBegin(FileInfoOf(entry), remaining));

    /// <inheritdoc />
    public FtpChunkEndAnswer EndChunk() =>
        chunkEnd is null ? defaults.EndChunk() : (FtpChunkEndAnswer)(int)chunkEnd();

    private static FtpChunkBeginAnswer BeginAnswerOf(ChunkBeginResult result) => result switch
    {
        ChunkBeginResult.Skip => FtpChunkBeginAnswer.Skip,
        ChunkBeginResult.Fail => FtpChunkBeginAnswer.Fail,
        _ => FtpChunkBeginAnswer.Transfer,
    };
}
