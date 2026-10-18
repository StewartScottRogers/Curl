namespace Libcurl.Global;

/// <summary>
/// <c>curl_malloc_callback</c>, the allocator <see cref="CurlGlobal.InitMemory"/> calls for the
/// allocations of its easy handles (ADR-0664).
/// </summary>
/// <param name="size">The number of bytes to allocate.</param>
/// <returns>The allocated block.</returns>
public delegate nint CurlMallocCallback(nuint size);
