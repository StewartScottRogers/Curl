namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// What <see cref="IFtpWildcardHooks.EndChunk" /> answers, as libcurl's
/// <c>CURL_CHUNK_END_FUNC_OK</c> and <c>CURL_CHUNK_END_FUNC_FAIL</c>. curl 8.22.0 ignores the
/// answer, and so does the wildcard download (ADR-0615).
/// </summary>
public enum FtpChunkEndAnswer
{
    /// <summary>The match was handled well.</summary>
    CarryOn = 0,

    /// <summary>The hook reports a failure, which the download does not act on.</summary>
    Fail = 1,
}
