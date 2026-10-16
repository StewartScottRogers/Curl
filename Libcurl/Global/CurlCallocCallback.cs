namespace Libcurl.Global;

/// <summary>
/// <c>curl_calloc_callback</c>, the zeroing allocator <see cref="CurlGlobal.InitMemory"/> accepts and
/// ignores: the managed engine allocates through the .NET runtime, never through it.
/// </summary>
/// <param name="count">The number of elements.</param>
/// <param name="size">The size of one element in bytes.</param>
/// <returns>The allocated, zeroed block.</returns>
public delegate nint CurlCallocCallback(nuint count, nuint size);
