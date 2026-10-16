using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;

namespace Libcurl.Multi;

/// <summary>
/// A multi handle (ADR-0506): <c>curl_multi_init</c>'s result, created by
/// <see cref="Global.CurlLibrary.MultiInit"/> over the engine's <see cref="IMultiTransfer"/>.
/// <see cref="AddHandle"/> and <see cref="RemoveHandle"/> are <c>curl_multi_add_handle</c> and
/// <c>_remove_handle</c>, <see cref="PerformAsync"/> is <c>curl_multi_perform</c>, <see cref="InfoRead"/>
/// is <c>curl_multi_info_read</c>, and <see cref="DisposeAsync"/> is <c>curl_multi_cleanup</c>.
/// </summary>
/// <remarks>
/// The class is <see langword="partial"/>: each multi area adds its members in its own folder under
/// <c>Libcurl/Multi</c>, keeping the namespace <c>Libcurl.Multi</c> (ADR-0567). Like libcurl's, a multi
/// handle is for one thread at a time.
/// </remarks>
public sealed partial class CurlMulti : IAsyncDisposable
{
    private readonly IMultiTransfer transfer;
    private readonly Dictionary<IEasyTransfer, CurlEasy> handles = new(ReferenceEqualityComparer.Instance);
    private int cleanedUp;

    /// <summary>Wraps the engine's multi <paramref name="transfer"/>.</summary>
    /// <param name="transfer">The engine's multi transfer; the handle disposes it on cleanup.</param>
    internal CurlMulti(IMultiTransfer transfer)
    {
        this.transfer = transfer;
    }

    /// <summary>Whether <see cref="DisposeAsync"/> (<c>curl_multi_cleanup</c>) has run.</summary>
    public bool IsCleanedUp => Volatile.Read(ref cleanedUp) != 0;

    /// <summary><c>curl_multi_add_handle</c>: adds <paramref name="easy"/>, whose transfer starts on the next perform.</summary>
    /// <param name="easy">The easy handle to add.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.BadEasyHandle"/> for a <see langword="null"/> or cleaned-up handle,
    /// <see cref="CurlMultiCode.AddedAlready"/> for one this handle holds already, else the engine's answer
    /// (<see cref="CurlMultiCode.AddedAlready"/> too when another multi handle holds it).
    /// </returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode AddHandle(CurlEasy? easy)
    {
        var live = LiveTransfer();
        if (easy is null || easy.IsCleanedUp)
        {
            return CurlMultiCode.BadEasyHandle;
        }

        var easyTransfer = easy.Transfer;
        if (handles.ContainsKey(easyTransfer))
        {
            return CurlMultiCode.AddedAlready;
        }

        var code = live.Add(easyTransfer);
        if (code == CurlMultiCode.Ok)
        {
            handles.Add(easyTransfer, easy);
        }

        return code;
    }

    /// <summary><c>curl_multi_remove_handle</c>: removes <paramref name="easy"/>, stopping its transfer if it still runs.</summary>
    /// <param name="easy">The easy handle to remove.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.BadEasyHandle"/> for a <see langword="null"/> or cleaned-up handle, else the
    /// engine's answer.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMultiCode RemoveHandle(CurlEasy? easy)
    {
        var live = LiveTransfer();
        if (easy is null || easy.IsCleanedUp)
        {
            return CurlMultiCode.BadEasyHandle;
        }

        var easyTransfer = easy.Transfer;
        var code = live.Remove(easyTransfer);
        if (code == CurlMultiCode.Ok)
        {
            handles.Remove(easyTransfer);
        }

        return code;
    }

    /// <summary><c>curl_multi_perform</c>: moves every added transfer on as far as it can go without waiting.</summary>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>The engine's result and <c>running_handles</c>, the transfers still running.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public ValueTask<(CurlMultiCode Code, int RunningHandles)> PerformAsync(CancellationToken cancellationToken = default) =>
        LiveTransfer().PerformAsync(cancellationToken);

    /// <summary><c>curl_multi_info_read</c>: takes the next message, if there is one.</summary>
    /// <param name="messagesInQueue"><c>msgs_in_queue</c>: the messages still waiting after this one; 0 when none was taken.</param>
    /// <returns>The message, about an easy handle added to this multi handle, or <see langword="null"/> when none waits.</returns>
    /// <exception cref="ObjectDisposedException">The multi handle has been cleaned up.</exception>
    public CurlMessage? InfoRead(out int messagesInQueue)
    {
        if (!LiveTransfer().TryReadInfo(out var message, out messagesInQueue))
        {
            return null;
        }

        return new CurlMessage(message.Type, handles[message.Transfer], message.Result);
    }

    /// <summary>
    /// <c>curl_multi_cleanup</c>: disposes the engine's multi transfer, which leaves every easy handle
    /// it held free to be added again. A second cleanup does nothing.
    /// </summary>
    /// <returns>A task that completes once the multi transfer is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref cleanedUp, 1) != 0)
        {
            return;
        }

        handles.Clear();
        await transfer.DisposeAsync().ConfigureAwait(false);
    }

    private IMultiTransfer LiveTransfer()
    {
        ObjectDisposedException.ThrowIf(IsCleanedUp, this);
        return transfer;
    }
}
