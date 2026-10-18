namespace Libcurl.Global;

/// <summary>
/// <c>curl_free_callback</c>, the deallocator <see cref="CurlGlobal.InitMemory"/> calls for the
/// allocations of its easy handles (ADR-0664).
/// </summary>
/// <param name="pointer">The block to free.</param>
public delegate void CurlFreeCallback(nint pointer);
