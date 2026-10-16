using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;

namespace Libcurl.Global;

/// <summary>
/// What <c>curl_global_init</c> sets up, as an object (ADR-0506): it holds the engine every handle
/// is created from, and <see cref="DisposeAsync"/> is <c>curl_global_cleanup</c>. Created by
/// <see cref="CurlGlobal.Init"/> or <see cref="CurlGlobal.InitMemory"/>.
/// </summary>
/// <remarks>
/// The class is <see langword="partial"/> so each handle area adds its public creating method
/// (<c>curl_easy_init</c>, <c>curl_multi_init</c>, <c>curl_share_init</c>, <c>curl_url</c>,
/// <c>curl_mime_init</c>, the first <c>curl_formadd</c>) in its own folder, over the internal
/// creating methods here, which check the library is still live and delegate to the engine
/// (ADR-0556).
/// </remarks>
public sealed partial class CurlLibrary : IAsyncDisposable
{
    private readonly ICurlEngine engine;
    private readonly GlobalInitCounter counter;
    private int cleanedUp;

    /// <summary>Counts one <c>curl_global_init</c> on <paramref name="counter"/> and holds <paramref name="engine"/>.</summary>
    /// <param name="engine">The engine handles are created from; the library disposes it on cleanup.</param>
    /// <param name="flags">The <c>CURL_GLOBAL_*</c> flags the library was initialised with.</param>
    /// <param name="counter">The <c>curl_global_init</c> counter to count on.</param>
    internal CurlLibrary(ICurlEngine engine, CurlGlobalFlags flags, GlobalInitCounter counter)
    {
        this.engine = engine;
        this.counter = counter;
        Flags = flags;
        counter.Increment();
    }

    /// <summary>The <c>CURL_GLOBAL_*</c> flags the library was initialised with, as given.</summary>
    public CurlGlobalFlags Flags { get; }

    /// <summary>Whether <see cref="DisposeAsync"/> (<c>curl_global_cleanup</c>) has run.</summary>
    public bool IsCleanedUp => Volatile.Read(ref cleanedUp) != 0;

    /// <summary>
    /// <c>curl_global_cleanup</c>: counts the cleanup and disposes the engine. A second cleanup of the
    /// same library does nothing, as libcurl's cleanup does once its counter is back at zero.
    /// </summary>
    /// <returns>A task that completes once the engine is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref cleanedUp, 1) != 0)
        {
            return;
        }

        counter.Decrement();
        await engine.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary><c>curl_easy_init</c>'s engine call.</summary>
    /// <returns>The engine's new easy transfer.</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    internal IEasyTransfer CreateEasyTransfer() => LiveEngine().CreateEasy();

    /// <summary><c>curl_multi_init</c>'s engine call.</summary>
    /// <returns>The engine's new multi transfer.</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    internal IMultiTransfer CreateMultiTransfer() => LiveEngine().CreateMulti();

    /// <summary><c>curl_share_init</c>'s engine call.</summary>
    /// <returns>The engine's new share handle.</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    internal IShareHandle CreateShareHandle() => LiveEngine().CreateShare();

    /// <summary><c>curl_url</c>'s engine call.</summary>
    /// <returns>The engine's new URL handle.</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    internal ICurlUrlHandle CreateUrlHandle() => LiveEngine().CreateUrl();

    /// <summary><c>curl_mime_init</c>'s engine call.</summary>
    /// <returns>The engine's new MIME builder.</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    internal IMimeBuilder CreateMimeBuilder() => LiveEngine().CreateMime();

    /// <summary>The engine call behind a form's first <c>curl_formadd</c>.</summary>
    /// <returns>The engine's new form builder.</returns>
    /// <exception cref="ObjectDisposedException">The library has been cleaned up.</exception>
    internal ICurlFormBuilder CreateFormBuilder() => LiveEngine().CreateForm();

    private ICurlEngine LiveEngine()
    {
        ObjectDisposedException.ThrowIf(IsCleanedUp, this);
        return engine;
    }
}
