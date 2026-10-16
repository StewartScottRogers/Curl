using Curl.Api.Codes;
using Curl.Api.EngineContracts;

namespace Libcurl.Global;

/// <summary>
/// libcurl's global functions (ADR-0506, ADR-0556): <c>curl_global_init</c> and
/// <c>curl_global_init_mem</c>, which return a <see cref="CurlLibrary"/> whose
/// <see cref="CurlLibrary.DisposeAsync"/> is <c>curl_global_cleanup</c>, and
/// <c>curl_global_sslset</c> and <c>curl_global_trace</c>, which answer as the build Curl matches
/// does: Schannel on Windows, OpenSSL elsewhere.
/// </summary>
public static class CurlGlobal
{
    private static readonly GlobalInitCounter Counter = new();

    /// <summary>
    /// libcurl's <c>initialized</c> count: the libraries <see cref="Init"/> and <see cref="InitMemory"/>
    /// have created in this process and not yet cleaned up.
    /// </summary>
    public static int InitCount => Counter.Count;

    /// <summary>
    /// <c>curl_global_init</c>: a library over <paramref name="engine"/>, counted until it is cleaned up.
    /// Every <paramref name="flags"/> value is accepted, as libcurl accepts it (see <see cref="CurlGlobalFlags"/>).
    /// </summary>
    /// <param name="engine">The engine every handle of the library is created from; the library disposes it on cleanup.</param>
    /// <param name="flags">The <c>CURL_GLOBAL_*</c> flags, kept on <see cref="CurlLibrary.Flags"/>.</param>
    /// <returns>The library; dispose it for <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <see langword="null"/>.</exception>
    public static CurlLibrary Init(ICurlEngine engine, CurlGlobalFlags flags = CurlGlobalFlags.Default) =>
        InitCounting(engine, flags, Counter);

    /// <summary>
    /// <c>curl_global_init_mem</c>: the same as <see cref="Init"/>. The five allocation callbacks are
    /// required, as libcurl requires them (it answers <c>CURLE_FAILED_INIT</c> for a missing one; this
    /// throws), and then ignored: the managed engine allocates, frees and copies through the .NET
    /// runtime, so none of them is ever called.
    /// </summary>
    /// <param name="engine">The engine every handle of the library is created from; the library disposes it on cleanup.</param>
    /// <param name="flags">The <c>CURL_GLOBAL_*</c> flags, kept on <see cref="CurlLibrary.Flags"/>.</param>
    /// <param name="malloc">The allocator; ignored.</param>
    /// <param name="free">The deallocator; ignored.</param>
    /// <param name="realloc">The reallocator; ignored.</param>
    /// <param name="strdup">The string duplicator; ignored.</param>
    /// <param name="calloc">The zeroing allocator; ignored.</param>
    /// <returns>The library; dispose it for <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">The engine or any callback is <see langword="null"/>.</exception>
    public static CurlLibrary InitMemory(
        ICurlEngine engine,
        CurlGlobalFlags flags,
        CurlMallocCallback malloc,
        CurlFreeCallback free,
        CurlReallocCallback realloc,
        CurlStrdupCallback strdup,
        CurlCallocCallback calloc) =>
        InitMemoryCounting(engine, flags, new MemoryCallbacks(malloc, free, realloc, strdup, calloc), Counter);

    /// <summary>
    /// <c>curl_global_sslset</c> as Curl's single-backend build answers it: <see cref="CurlSslSet.Ok"/>
    /// when <paramref name="id"/> is the build's backend or <paramref name="name"/> names it (ignoring
    /// case), else <see cref="CurlSslSet.TooLate"/>, since the one backend is already chosen.
    /// </summary>
    /// <param name="id">The backend asked for, or <see cref="CurlSslBackendId.None"/> to match by name.</param>
    /// <param name="name">The backend's name, or <see langword="null"/> to match by id.</param>
    /// <param name="available">The backends built in: the one backend of the build.</param>
    /// <returns>The <c>CURLSSLSET_*</c> answer.</returns>
    public static CurlSslSet SslSet(CurlSslBackendId id, string? name, out IReadOnlyList<CurlSslBackend> available) =>
        SslSetFor(id, name, CurlSslBackend.Current, out available);

    /// <summary>
    /// <c>curl_global_trace</c>: accepts any trace configuration and answers <see cref="CurlCode.Ok"/>,
    /// as libcurl 8.22.0 does, which skips names it does not know. The managed engine has no trace
    /// components to switch, so the configuration changes nothing.
    /// </summary>
    /// <param name="configuration">The comma-separated component names, each optionally prefixed by <c>+</c> or <c>-</c>; may be <see langword="null"/>.</param>
    /// <returns><see cref="CurlCode.Ok"/>.</returns>
    public static CurlCode Trace(string? configuration) => CurlCode.Ok;

    /// <summary><see cref="Init(ICurlEngine, CurlGlobalFlags)"/> counting on <paramref name="counter"/>.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="counter">The <c>curl_global_init</c> counter.</param>
    /// <returns>The library.</returns>
    internal static CurlLibrary InitCounting(ICurlEngine engine, CurlGlobalFlags flags, GlobalInitCounter counter)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return new CurlLibrary(engine, flags, counter);
    }

    /// <summary><see cref="InitMemory(ICurlEngine, CurlGlobalFlags, CurlMallocCallback, CurlFreeCallback, CurlReallocCallback, CurlStrdupCallback, CurlCallocCallback)"/> counting on <paramref name="counter"/>.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="callbacks">The five allocation callbacks, checked and ignored.</param>
    /// <param name="counter">The <c>curl_global_init</c> counter.</param>
    /// <returns>The library.</returns>
    internal static CurlLibrary InitMemoryCounting(ICurlEngine engine, CurlGlobalFlags flags, MemoryCallbacks callbacks, GlobalInitCounter counter)
    {
        callbacks.ThrowIfAnyNull();
        return InitCounting(engine, flags, counter);
    }

    /// <summary><see cref="SslSet(CurlSslBackendId, string?, out IReadOnlyList{CurlSslBackend})"/> for a build whose one backend is <paramref name="built"/>.</summary>
    /// <param name="id">The backend id asked for.</param>
    /// <param name="name">The backend name asked for.</param>
    /// <param name="built">The build's backend.</param>
    /// <param name="available">The backends built in.</param>
    /// <returns>The <c>CURLSSLSET_*</c> answer.</returns>
    internal static CurlSslSet SslSetFor(CurlSslBackendId id, string? name, CurlSslBackend built, out IReadOnlyList<CurlSslBackend> available)
    {
        available = [built];
        var matches = id == built.Id || string.Equals(name, built.Name, StringComparison.OrdinalIgnoreCase);
        return matches ? CurlSslSet.Ok : CurlSslSet.TooLate;
    }
}
