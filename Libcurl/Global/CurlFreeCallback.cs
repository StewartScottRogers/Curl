namespace Libcurl.Global;

/// <summary>
/// <c>curl_free_callback</c>, the deallocator <see cref="CurlGlobal.InitMemory"/> accepts and ignores:
/// the managed engine frees through the .NET runtime, never through it.
/// </summary>
/// <param name="pointer">The block to free.</param>
public delegate void CurlFreeCallback(nint pointer);
