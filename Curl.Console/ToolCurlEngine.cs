using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Engine;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Multi;

namespace Curl.Console;

/// <summary>
/// The curl tool's view of <c>Libcurl</c> (ADR-0506, ADR-0624): the runner's engine behind a
/// <see cref="CurlLibrary" />, so a serial transfer's easy handle comes from <c>curl_easy_init</c>
/// (<see cref="EasyInit" />) as upstream's tool gets its handles, while the runner still reaches the
/// engine's <see cref="IToolEasyTransfer" /> under it for the tool's own hooks, which libcurl's options have
/// no <c>CURLOPT_*</c> for. Every other engine call passes straight to the runner's engine.
/// </summary>
internal sealed class ToolCurlEngine : ICurlEngine
{
    private readonly ICurlEngine engine;

    private readonly CurlLibrary library;

    private readonly Lock easyInitLock = new();

    private IToolEasyTransfer? createdEasy;

    /// <summary>Wraps <paramref name="engine" /> and initialises the library over it (<c>curl_global_init</c>).</summary>
    /// <param name="engine">The runner's engine, whose easy transfers are <see cref="IToolEasyTransfer" />s.</param>
    internal ToolCurlEngine(ICurlEngine engine)
    {
        this.engine = engine;
        library = CurlGlobal.Init(this);
    }

    /// <inheritdoc />
    public CurlVersionInfo VersionInfo => engine.VersionInfo;

    /// <summary>
    /// <c>curl_easy_init</c> on the library, paired with the engine transfer the new handle wraps.
    /// </summary>
    /// <returns>The handle and its tool hooks; disposing it is <c>curl_easy_cleanup</c>.</returns>
    /// <remarks>
    /// The engine transfer is kept in a field between <see cref="CreateEasy" /> and the pairing, so the pair is
    /// made under a lock: a <c>-Z</c> run's transfers ask for their handles from several flows at once.
    /// </remarks>
    internal ToolEasyHandle EasyInit()
    {
        lock (easyInitLock)
        {
            CurlEasy easy = library.EasyInit();
            return new ToolEasyHandle(easy, createdEasy!);
        }
    }

    /// <summary><c>curl_multi_init</c> on the library, for a <c>-Z</c> run's transfers (ADR-0628).</summary>
    /// <returns>The multi handle; disposing it is <c>curl_multi_cleanup</c>.</returns>
    internal CurlMulti MultiInit() => library.MultiInit();

    /// <summary><c>curl_mime_init</c> on the library, for a transfer's <c>-F</c> form (BL-2493, ADR-0652).</summary>
    /// <returns>The empty form; disposing it is <c>curl_mime_free</c>.</returns>
    internal CurlMime MimeInit() => library.MimeInit();

    /// <summary>Creates the engine's easy transfer and keeps it for <see cref="EasyInit" /> to pair with its handle.</summary>
    /// <returns>The engine's transfer.</returns>
    public IEasyTransfer CreateEasy()
    {
        IToolEasyTransfer created = (IToolEasyTransfer)engine.CreateEasy();
        createdEasy = created;
        return created;
    }

    /// <inheritdoc />
    public IMultiTransfer CreateMulti() => engine.CreateMulti();

    /// <inheritdoc />
    public IShareHandle CreateShare() => engine.CreateShare();

    /// <inheritdoc />
    public ICurlUrlHandle CreateUrl() => engine.CreateUrl();

    /// <inheritdoc />
    public IMimeBuilder CreateMime() => engine.CreateMime();

    /// <inheritdoc />
    public ICurlFormBuilder CreateForm() => engine.CreateForm();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => engine.DisposeAsync();
}
