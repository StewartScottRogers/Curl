namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_HSTSREADFUNCTION</c>: hands libcurl one HSTS entry per call to preload its cache.
/// <c>CURLOPT_HSTSREADDATA</c> is the delegate's closure.
/// </summary>
/// <param name="entry">The entry handed over, or <see langword="null"/> when there is none.</param>
/// <returns><see cref="HstsResult.Ok"/> to be called again, <see cref="HstsResult.Done"/> when no entries are left, or <see cref="HstsResult.Fail"/>.</returns>
public delegate HstsResult HstsReadCallback(out HstsEntry? entry);
