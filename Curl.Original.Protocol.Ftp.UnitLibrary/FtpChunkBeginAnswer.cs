namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// What <see cref="IFtpWildcardHooks.BeginChunk" /> answers, as libcurl's
/// <c>CURL_CHUNK_BGN_FUNC_OK</c>, <c>CURL_CHUNK_BGN_FUNC_FAIL</c> and <c>CURL_CHUNK_BGN_FUNC_SKIP</c>.
/// </summary>
public enum FtpChunkBeginAnswer
{
    /// <summary>Download the match, when it is a file.</summary>
    Transfer = 0,

    /// <summary>End the whole download with exit 88 (<c>CURLE_CHUNK_FAILED</c>) and no <c>QUIT</c>.</summary>
    Fail = 1,

    /// <summary>Skip the match and go on to the next one.</summary>
    Skip = 2,
}
