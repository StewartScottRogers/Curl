namespace Libcurl.Global;

/// <summary>
/// <c>curl_malloc_callback</c>, the allocator <see cref="CurlGlobal.InitMemory"/> accepts and ignores:
/// the managed engine allocates through the .NET runtime, never through it.
/// </summary>
/// <param name="size">The number of bytes to allocate.</param>
/// <returns>The allocated block.</returns>
public delegate nint CurlMallocCallback(nuint size);
