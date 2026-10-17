namespace Libcurl.Global;

/// <summary>The five allocation callbacks <c>curl_global_init_mem</c> takes, which its easy handles allocate through (ADR-0664).</summary>
/// <param name="Malloc">The allocator.</param>
/// <param name="Free">The deallocator.</param>
/// <param name="Realloc">The reallocator.</param>
/// <param name="Strdup">The string duplicator.</param>
/// <param name="Calloc">The zeroing allocator.</param>
internal sealed record MemoryCallbacks(
    CurlMallocCallback Malloc,
    CurlFreeCallback Free,
    CurlReallocCallback Realloc,
    CurlStrdupCallback Strdup,
    CurlCallocCallback Calloc)
{
    /// <summary>Throws when any callback is missing, where libcurl answers <c>CURLE_FAILED_INIT</c>.</summary>
    /// <exception cref="ArgumentNullException">A callback is <see langword="null"/>.</exception>
    internal void ThrowIfAnyNull()
    {
        ArgumentNullException.ThrowIfNull(Malloc, "malloc");
        ArgumentNullException.ThrowIfNull(Free, "free");
        ArgumentNullException.ThrowIfNull(Realloc, "realloc");
        ArgumentNullException.ThrowIfNull(Strdup, "strdup");
        ArgumentNullException.ThrowIfNull(Calloc, "calloc");
    }
}
