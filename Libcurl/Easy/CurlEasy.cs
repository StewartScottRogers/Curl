using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Libcurl.Info;
using Libcurl.Options;

namespace Libcurl.Easy;

/// <summary>
/// An easy handle (ADR-0506): <c>curl_easy_init</c>'s result, created by
/// <see cref="Global.CurlLibrary.EasyInit"/> over the engine's <see cref="IEasyTransfer"/>.
/// <see cref="SetOption"/> is <c>curl_easy_setopt</c>, <see cref="GetInfo"/> is <c>curl_easy_getinfo</c>, <see cref="Perform"/> and
/// <see cref="PerformAsync"/> are <c>curl_easy_perform</c>, and <see cref="Dispose"/> (or
/// <see cref="DisposeAsync"/>) is <c>curl_easy_cleanup</c>.
/// </summary>
public sealed class CurlEasy : IDisposable, IAsyncDisposable
{
    private readonly IEasyTransfer transfer;
    private readonly EasyOptionDispatch dispatch;
    private int cleanedUp;

    /// <summary>Wraps the engine's <paramref name="transfer"/>, setting options through <paramref name="dispatch"/>.</summary>
    /// <param name="transfer">The engine's easy transfer; the handle disposes it on cleanup.</param>
    /// <param name="dispatch">The <c>curl_easy_setopt</c> dispatch.</param>
    internal CurlEasy(IEasyTransfer transfer, EasyOptionDispatch dispatch)
    {
        this.transfer = transfer;
        this.dispatch = dispatch;
    }

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

    /// <summary><c>curl_easy_perform</c>: runs the transfer the handle's options describe.</summary>
    /// <param name="cancellationToken">Stops the transfer; it then ends with the engine's error code.</param>
    /// <returns>The engine's result for the transfer.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken = default) =>
        LiveTransfer().PerformAsync(cancellationToken);

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
    }

    private IEasyTransfer LiveTransfer()
    {
        ObjectDisposedException.ThrowIf(IsCleanedUp, this);
        return transfer;
    }
}
