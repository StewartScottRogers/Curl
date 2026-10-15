namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_CHUNK_END_FUNCTION</c>: called after each file of a wildcard transfer, transferred
/// or skipped. <c>CURLOPT_CHUNK_DATA</c> is the delegate's closure.
/// </summary>
/// <returns>Whether to carry on or fail.</returns>
public delegate ChunkEndResult ChunkEndCallback();
