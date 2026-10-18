namespace Libcurl.Global;

/// <summary>
/// <c>curl_realloc_callback</c>, the reallocator <see cref="CurlGlobal.InitMemory"/> accepts and never
/// calls: no Libcurl call reallocates (ADR-0664).
/// </summary>
/// <param name="pointer">The block to resize.</param>
/// <param name="size">Its new size in bytes.</param>
/// <returns>The resized block.</returns>
public delegate nint CurlReallocCallback(nint pointer, nuint size);
