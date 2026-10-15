namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_HSTSWRITEFUNCTION</c>: takes libcurl's HSTS cache one entry per call when it is saved.
/// <c>CURLOPT_HSTSWRITEDATA</c> is the delegate's closure.
/// </summary>
/// <param name="entry">The cache entry.</param>
/// <param name="index">The entry's zero-based position (upstream's <c>curl_index.index</c>).</param>
/// <param name="total">How many entries the cache holds (upstream's <c>curl_index.total</c>).</param>
/// <returns><see cref="HstsResult.Ok"/> to be called with the next entry, <see cref="HstsResult.Done"/> to stop, or <see cref="HstsResult.Fail"/>.</returns>
public delegate HstsResult HstsWriteCallback(HstsEntry entry, int index, int total);
