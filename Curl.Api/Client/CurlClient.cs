using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Errors;
using Curl.Api.Options;

namespace Curl.Api;

/// <summary>
/// The entry point of <c>Curl.Api</c> (ADR-0506): a client over one <see cref="ICurlEngine"/>
/// that creates an easy handle for each transfer, with <see cref="Options"/> applied, and runs its
/// transfers together on one multi handle (ADR-0644). Disposing it cancels every running transfer and
/// disposes the engine when the client owns it; after <see cref="DisposeAsync"/> every member
/// that uses the engine throws <see cref="ObjectDisposedException"/>.
/// </summary>
public sealed partial class CurlClient : IAsyncDisposable
{
    private readonly ICurlEngine _engine;
    private readonly bool _ownsEngine;
    private int _disposed;

    /// <summary>A client that owns <paramref name="engine"/> and uses the default <see cref="CurlClientOptions"/>.</summary>
    /// <param name="engine">The engine; disposed with the client.</param>
    public CurlClient(ICurlEngine engine)
        : this(engine, new CurlClientOptions(), ownsEngine: true)
    {
    }

    /// <summary>A client over <paramref name="engine"/> with the given defaults.</summary>
    /// <param name="engine">The engine every transfer runs on.</param>
    /// <param name="options">The defaults applied to every easy handle the client creates.</param>
    /// <param name="ownsEngine"><see langword="true"/> to dispose <paramref name="engine"/> with the client; <see langword="false"/> to leave it to the caller.</param>
    public CurlClient(ICurlEngine engine, CurlClientOptions options, bool ownsEngine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(options);
        _engine = engine;
        Options = options;
        _ownsEngine = ownsEngine;
    }

    /// <summary>The defaults applied to every easy handle the client creates.</summary>
    public CurlClientOptions Options { get; }

    /// <summary>Whether the engine runs Curl's extensions: <see cref="Create(CurlClientOptions)"/> with <c>CURL_EXTENSIONS</c> not <c>0</c> (ADR-1319). A request's extension options fail with <see cref="CurlCode.UnknownOption"/> otherwise.</summary>
    internal bool AcceptsExtensionOptions { get; init; }

    /// <summary>
    /// <paramref name="options"/> with the extension options <paramref name="request"/> sets (<see cref="CurlRequest.Dns"/>).
    /// </summary>
    /// <param name="options">The handle's options so far.</param>
    /// <param name="request">The request.</param>
    /// <returns>The options with the extension options set; <paramref name="options"/> itself when the request sets none.</returns>
    /// <exception cref="CurlException">The request sets an extension option and the client's extensions are off.</exception>
    private EasyOptions ApplyExtensionOptions(EasyOptions options, CurlRequest request)
    {
        if (request.Dns is null)
        {
            return options;
        }

        return AcceptsExtensionOptions
            ? options with { ExtensionOptions = request.Dns.ToExtensionOptions() }
            : throw new CurlException(CurlCode.UnknownOption, "unknown option: --dns-json / --dns-transport (extensions are off)");
    }

    /// <summary>Whether <see cref="DisposeAsync"/> also disposes the engine.</summary>
    public bool OwnsEngine => _ownsEngine;

    /// <summary>
    /// <c>curl_easy_init</c> on the client's engine, then <see cref="Options"/> applied to the new
    /// handle. The caller disposes the handle.
    /// </summary>
    /// <returns>The new easy handle.</returns>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    internal IEasyTransfer CreateEasy()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        IEasyTransfer easy = _engine.CreateEasy();
        EasyOptions defaults = easy.Options;
        easy.Options = defaults with
        {
            Http = defaults.Http with { UserAgent = Options.UserAgent },
            Transfer = defaults.Transfer with { Timeout = Options.Timeout, FileTime = Options.FetchFileTime },
            Network = defaults.Network with { ConnectTimeout = Options.ConnectTimeout },
        };
        easy.Options = CurlTransportOptions.Apply(easy.Options, Options.Network, Options.Tls, Options.Proxy);
        easy.Options = CurlProtocolOptions.Apply(easy.Options, Options.ProtocolRecords);
        return BindStores(easy);
    }

    /// <summary>
    /// Cancels every transfer still running (each <c>SendAsync</c> call or body read then throws
    /// <see cref="OperationCanceledException"/>), disposes the multi handle they ran on, then disposes
    /// the client's own <see cref="Stores"/>, if it made any, and the engine when the client owns it.
    /// Safe to call more than once; only the first call disposes anything.
    /// </summary>
    /// <returns>A task that completes when the multi handle and the engine (if owned) are disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopTransfersAsync().ConfigureAwait(false);
        DisposeOwnStores();
        if (_ownsEngine)
        {
            await _engine.DisposeAsync().ConfigureAwait(false);
        }
    }
}
