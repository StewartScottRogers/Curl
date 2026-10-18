using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Mime;
using Libcurl.Options;

namespace Libcurl.Easy;

/// <summary>
/// An easy handle (ADR-0506): <c>curl_easy_init</c>'s result, created by
/// <see cref="Global.CurlLibrary.EasyInit"/> over the engine's <see cref="IEasyTransfer"/>.
/// <see cref="SetOption"/> is <c>curl_easy_setopt</c>, <see cref="GetInfo"/> is <c>curl_easy_getinfo</c>, <see cref="Perform"/> and
/// <see cref="PerformAsync"/> are <c>curl_easy_perform</c>, <see cref="Reset"/> is <c>curl_easy_reset</c>,
/// <see cref="DupHandle"/> is <c>curl_easy_duphandle</c>, <see cref="Pause"/> is <c>curl_easy_pause</c>,
/// <see cref="Upkeep"/> and <see cref="UpkeepAsync"/> are <c>curl_easy_upkeep</c>, <see cref="Recv"/> and
/// <see cref="RecvAsync"/> are <c>curl_easy_recv</c>, <see cref="Send"/> and <see cref="SendAsync"/> are
/// <c>curl_easy_send</c>, and <see cref="Dispose"/> (or
/// <see cref="DisposeAsync"/>) is <c>curl_easy_cleanup</c>.
/// </summary>
public sealed class CurlEasy : IDisposable, IAsyncDisposable
{
    private readonly IEasyTransfer transfer;
    private readonly EasyOptionDispatch dispatch;
    private readonly EasyAllocations allocations;
    private CurlMime? boundMime;
    private int cleanedUp;

    /// <summary>Wraps the engine's <paramref name="transfer"/>, setting options through <paramref name="dispatch"/>.</summary>
    /// <param name="transfer">The engine's easy transfer; the handle disposes it on cleanup.</param>
    /// <param name="dispatch">The <c>curl_easy_setopt</c> dispatch.</param>
    /// <param name="allocations">The handle's <c>curl_global_init_mem</c> allocations; the handle frees them on cleanup.</param>
    internal CurlEasy(IEasyTransfer transfer, EasyOptionDispatch dispatch, EasyAllocations? allocations = null)
    {
        this.transfer = transfer;
        this.dispatch = dispatch;
        this.allocations = allocations ?? EasyAllocations.None;
    }

    /// <summary>The handle's <c>curl_global_init_mem</c> allocations, which <c>curl_easy_escape</c> adds to.</summary>
    internal EasyAllocations Allocations => allocations;

    /// <summary>Whether <see cref="Dispose"/> or <see cref="DisposeAsync"/> (<c>curl_easy_cleanup</c>) has run.</summary>
    public bool IsCleanedUp => Volatile.Read(ref cleanedUp) != 0;

    /// <summary>The handle's options as <see cref="SetOption"/> has left them, as the engine holds them.</summary>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public EasyOptions Options => LiveTransfer().Options;

    /// <summary>The engine's transfer, which <see cref="Multi.CurlMulti"/> adds to and removes from its multi transfer.</summary>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    internal IEasyTransfer Transfer => LiveTransfer();

    /// <summary><c>curl_easy_setopt</c>: sets <paramref name="option"/> to <paramref name="value"/>.</summary>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The value, of the .NET type the option's <see cref="CurlOptionType"/> takes.</param>
    /// <returns>
    /// <see cref="CurlCode.Ok"/> once the option is set, else the code <c>curl_easy_setopt</c> answers
    /// (<see cref="CurlCode.UnknownOption"/>, <see cref="CurlCode.BadFunctionArgument"/>,
    /// <see cref="CurlCode.NotBuiltIn"/> or the option's own); the options are then unchanged.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode SetOption(CurlOption option, object? value)
    {
        var live = LiveTransfer();
        var options = live.Options;
        var code = dispatch.SetOption(ref options, option, value);
        if (code == CurlCode.Ok)
        {
            live.Options = options;
            StrdupString(value);
            BindMime(option, value);
        }

        return code;
    }

    /// <summary><c>curl_easy_getinfo</c>: reads <paramref name="info"/> about the handle's last transfer.</summary>
    /// <param name="info">The info to read.</param>
    /// <param name="value">
    /// The value, of the .NET type <see cref="CurlInfo"/>'s remarks give for the info's base; <see langword="null"/>
    /// when the answer is not <see cref="CurlCode.Ok"/>.
    /// </param>
    /// <returns>
    /// <see cref="CurlCode.Ok"/>, <see cref="CurlCode.UnknownOption"/> for an info <c>curl.h</c> lacks, or
    /// <see cref="CurlCode.NotBuiltIn"/> for one Libcurl does not read yet.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode GetInfo(CurlInfo info, out object? value)
    {
        var live = LiveTransfer();
        return EasyInfoDispatch.Default.GetInfo(info, live.Info, live.Options, out value);
    }

    /// <summary>
    /// <c>curl_easy_perform</c>, synchronously as libcurl's is: blocks on <see cref="PerformAsync"/>
    /// until the transfer ends. This is the one sanctioned sync-over-async bridge in Libcurl, kept for
    /// fidelity with libcurl's blocking call; callers that can await use <see cref="PerformAsync"/>.
    /// </summary>
    /// <returns>The transfer's result.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode Perform() => PerformAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// <c>curl_easy_perform</c>: runs the transfer the handle's options describe. A <c>CURLOPT_MIMEPOST</c>
    /// structure is read as its parts are now, so a part changed since the setopt sends its new data; once
    /// <c>curl_mime_free</c> has freed it, the handle posts an empty body, as libcurl's unbound
    /// <c>MIMEKIND_NONE</c> part does (upstream test653 and test654, ADR-0675).
    /// </summary>
    /// <param name="cancellationToken">Stops the transfer; it then ends with the engine's error code.</param>
    /// <returns>The engine's result for the transfer.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken = default) =>
        PerformFillingErrorBufferAsync(LiveTransferWithMimeAsNow(), cancellationToken);

    /// <summary>
    /// The text of the last perform's error as <c>CURLOPT_ERRORBUFFER</c> holds it: the engine's text cut
    /// to 255 UTF-8 bytes; empty when the last perform succeeded or gave no detail.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public string ErrorText => EasyErrorBuffer.Cut(LiveTransfer().ErrorText);

    /// <summary>
    /// <c>curl_easy_reset</c>: every option back to its default, as a new handle has them. The engine
    /// keeps the handle's live connections, DNS cache and session cache, as libcurl does.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public void Reset()
    {
        LiveTransfer().Reset();
        boundMime = null;
    }

    /// <summary>
    /// <c>curl_easy_duphandle</c>: a new handle with this one's options and none of its state - no
    /// connection, no last transfer's info, no received headers - over the engine's duplicate transfer.
    /// The two handles are independent: setting an option on one leaves the other's unchanged. A
    /// <c>CURLOPT_MIMEPOST</c> structure is copied as its parts are now, as libcurl's <c>Curl_mime_duppart</c>
    /// copies it, so the duplicate posts that copy whatever later happens to the structure.
    /// </summary>
    /// <returns>The new handle; the caller disposes it (<c>curl_easy_cleanup</c>).</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlEasy DupHandle() => new(LiveTransferWithMimeAsNow().Duplicate(), dispatch, allocations.ForDuplicate());

    /// <summary>
    /// <c>curl_easy_pause</c>: leaves the directions set in <paramref name="directions"/> paused and
    /// unpauses the others. Bits other than <see cref="CurlPause.Receive"/> and <see cref="CurlPause.Send"/>
    /// are dropped before the engine sees them, as libcurl reads only those two.
    /// </summary>
    /// <param name="directions">The <c>CURLPAUSE_*</c> bitmask; <see cref="CurlPause.Continue"/> unpauses both directions.</param>
    /// <returns>The engine's answer.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode Pause(CurlPause directions) => LiveTransfer().Pause(directions & CurlPause.All);

    /// <summary>
    /// <c>curl_easy_upkeep</c>, synchronously as libcurl's is: blocks on <see cref="UpkeepAsync"/>. Like
    /// <see cref="Perform"/>, a sanctioned sync-over-async bridge kept for fidelity with libcurl's blocking call.
    /// </summary>
    /// <returns>The engine's answer.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode Upkeep() => UpkeepAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

    /// <summary><c>curl_easy_upkeep</c>: keeps the handle's idle connections alive.</summary>
    /// <param name="cancellationToken">Stops the upkeep.</param>
    /// <returns>The engine's answer.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken = default) =>
        LiveTransfer().UpkeepAsync(cancellationToken);

    /// <summary>
    /// <c>curl_easy_recv</c>: receives the bytes the connection a <c>CURLOPT_CONNECT_ONLY</c> perform left open has
    /// ready, blocking on <see cref="RecvAsync"/>, which itself never waits for the peer.
    /// </summary>
    /// <param name="buffer">Where the received bytes are written.</param>
    /// <param name="received"><c>*n</c>: how many bytes were written; 0 unless the answer is <see cref="CurlCode.Ok"/>, and 0 with it once the peer has closed.</param>
    /// <returns>
    /// The engine's answer: <see cref="CurlCode.Ok"/>, <see cref="CurlCode.Again"/> when nothing is ready yet, or
    /// <see cref="CurlCode.UnsupportedProtocol"/> when no connect-only perform left a connection open.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode Recv(Memory<byte> buffer, out int received)
    {
        var result = RecvAsync(buffer).AsTask().GetAwaiter().GetResult();
        received = result.Result == CurlCode.Ok ? result.ByteCount : 0;
        return result.Result;
    }

    /// <summary><c>curl_easy_recv</c>: receives the bytes the connect-only connection has ready, without waiting for more.</summary>
    /// <param name="buffer">Where the received bytes are written.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The engine's result and byte count, as <see cref="Recv"/> describes.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public ValueTask<CurlConnectOnlyResult> RecvAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        LiveTransfer().Connection.ReceiveAsync(buffer, cancellationToken);

    /// <summary>
    /// <c>curl_easy_send</c>: sends <paramref name="buffer"/> on the connection a <c>CURLOPT_CONNECT_ONLY</c> perform
    /// left open, blocking on <see cref="SendAsync"/>, which itself never waits for the peer.
    /// </summary>
    /// <param name="buffer">The bytes to send.</param>
    /// <param name="sent"><c>*n</c>: how many bytes of <paramref name="buffer"/> were sent; 0 unless the answer is <see cref="CurlCode.Ok"/>.</param>
    /// <returns>
    /// The engine's answer: <see cref="CurlCode.Ok"/>, <see cref="CurlCode.Again"/> when nothing could be sent yet, or
    /// <see cref="CurlCode.UnsupportedProtocol"/> when no connect-only perform left a connection open.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode Send(ReadOnlyMemory<byte> buffer, out int sent)
    {
        var result = SendAsync(buffer).AsTask().GetAwaiter().GetResult();
        sent = result.Result == CurlCode.Ok ? result.ByteCount : 0;
        return result.Result;
    }

    /// <summary><c>curl_easy_send</c>: sends as many bytes of <paramref name="buffer"/> as the connect-only connection takes now.</summary>
    /// <param name="buffer">The bytes to send.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The engine's result and byte count, as <see cref="Send"/> describes.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public ValueTask<CurlConnectOnlyResult> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        LiveTransfer().Connection.SendAsync(buffer, cancellationToken);

    /// <summary>
    /// <c>curl_easy_cleanup</c>, synchronously: blocks on <see cref="DisposeAsync"/>. A second cleanup
    /// does nothing.
    /// </summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary><c>curl_easy_cleanup</c>: disposes the engine's transfer. A second cleanup does nothing.</summary>
    /// <returns>A task that completes once the transfer is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref cleanedUp, 1) != 0)
        {
            return;
        }

        await transfer.DisposeAsync().ConfigureAwait(false);
        allocations.FreeAll();
    }

    private static async ValueTask<CurlCode> PerformFillingErrorBufferAsync(IEasyTransfer live, CancellationToken cancellationToken)
    {
        var buffer = live.Options.Behaviour.ErrorBuffer;
        var code = await live.PerformAsync(cancellationToken).ConfigureAwait(false);
        EasyErrorBuffer.Fill(buffer, code, live.ErrorText);
        return code;
    }

    /// <summary>
    /// The options with the bound <c>CURLOPT_MIMEPOST</c> structure's body as its parts are now, or, once it is
    /// freed, an empty <see cref="EasyHttpOptions.HttpPost"/> form in its place, which the engine posts as a
    /// zero-length body with no <c>Content-Type</c>, as libcurl posts its unbound part.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <param name="mime">The bound structure.</param>
    /// <returns>The options to perform with.</returns>
    private static EasyOptions WithMimeAsNow(EasyOptions options, CurlMime mime) =>
        options with
        {
            Http = mime.IsFreed
                ? options.Http with { MimePost = null, HttpPost = new EasyMimeBody() }
                : options.Http with { MimePost = mime.Build() },
        };

    /// <summary>
    /// Remembers the structure a successful <c>CURLOPT_MIMEPOST</c> bound, so each perform reads it as it is then;
    /// a <c>NULL</c> MIME post or a <c>CURLOPT_HTTPPOST</c> form, which libcurl then sends instead, unbinds it.
    /// </summary>
    /// <param name="option">The option set.</param>
    /// <param name="value">Its value.</param>
    private void BindMime(CurlOption option, object? value)
    {
        if (option is CurlOption.Mimepost or CurlOption.Httppost)
        {
            boundMime = value as CurlMime;
        }
    }

    /// <summary>The live transfer, its options carrying the bound MIME structure as it is now.</summary>
    /// <returns>The transfer.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    private IEasyTransfer LiveTransferWithMimeAsNow()
    {
        var live = LiveTransfer();
        if (boundMime is { } mime)
        {
            live.Options = WithMimeAsNow(live.Options, mime);
        }

        return live;
    }

    private void StrdupString(object? value)
    {
        if (value is string text)
        {
            allocations.Strdup(text);
        }
    }

    private IEasyTransfer LiveTransfer()
    {
        ObjectDisposedException.ThrowIf(IsCleanedUp, this);
        return transfer;
    }
}
