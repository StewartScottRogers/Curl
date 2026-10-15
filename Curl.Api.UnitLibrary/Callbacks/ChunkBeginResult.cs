namespace Curl.Api.Callbacks;

/// <summary>What a <see cref="ChunkBeginCallback"/> returns: libcurl's <c>CURL_CHUNK_BGN_FUNC_*</c>, with upstream curl 8.22.0's numbers.</summary>
public enum ChunkBeginResult
{
    /// <summary><c>CURL_CHUNK_BGN_FUNC_OK</c> (0): transfer this file.</summary>
    Ok = 0,

    /// <summary><c>CURL_CHUNK_BGN_FUNC_FAIL</c> (1): fail the transfer.</summary>
    Fail = 1,

    /// <summary><c>CURL_CHUNK_BGN_FUNC_SKIP</c> (2): skip this file.</summary>
    Skip = 2,
}
