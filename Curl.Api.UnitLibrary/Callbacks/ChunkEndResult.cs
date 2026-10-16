namespace Curl.Api.Callbacks;

/// <summary>What a <see cref="ChunkEndCallback"/> returns: libcurl's <c>CURL_CHUNK_END_FUNC_*</c>, with upstream curl 8.22.0's numbers.</summary>
public enum ChunkEndResult
{
    /// <summary><c>CURL_CHUNK_END_FUNC_OK</c> (0): carry on with the next file.</summary>
    Ok = 0,

    /// <summary><c>CURL_CHUNK_END_FUNC_FAIL</c> (1): fail the transfer.</summary>
    Fail = 1,
}
