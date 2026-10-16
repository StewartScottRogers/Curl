namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_CHUNK_BGN_FUNCTION</c>: called before each file of a wildcard transfer.
/// <c>CURLOPT_CHUNK_DATA</c> is the delegate's closure.
/// </summary>
/// <param name="file">The file about to be transferred.</param>
/// <param name="remains">How many files are left after this one.</param>
/// <returns>Whether to transfer, skip or fail.</returns>
public delegate ChunkBeginResult ChunkBeginCallback(CurlFileInfo file, int remains);
