namespace Libcurl.Global;

/// <summary>
/// <c>curl_realloc_callback</c>, the reallocator <see cref="CurlGlobal.InitMemory"/> accepts and ignores:
/// the managed engine reallocates through the .NET runtime, never through it.
/// </summary>
/// <param name="pointer">The block to resize.</param>
/// <param name="size">Its new size in bytes.</param>
/// <returns>The resized block.</returns>
public delegate nint CurlReallocCallback(nint pointer, nuint size);
