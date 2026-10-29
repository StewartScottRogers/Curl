using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

namespace Curl.Engine;

internal sealed partial class ManagedMultiTransfer
{
    /// <summary>
    /// <c>curl_multi_add_handle</c>: adds <paramref name="transfer" />, which starts on the next
    /// <see cref="PerformAsync" />.
    /// </summary>
    /// <param name="transfer">The transfer to add.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />; <see cref="CurlMultiCode.AddedAlready" /> when it is in a multi handle
    /// of the engine already, <see cref="CurlMultiCode.BadEasyHandle" /> for <see langword="null" /> and
    /// <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed. As libcurl does, it gives the
    /// timer callback 0 (act at once), and answers <see cref="CurlMultiCode.AbortedByCallback" /> when that
    /// returns -1. Called from inside one of its own transfers' callbacks (<see cref="IsInTransferCallback" />)
    /// it adds nothing and answers <see cref="CurlMultiCode.RecursiveApiCall" />, as libcurl does (CGR-146).
    /// </returns>
    public CurlMultiCode Add(IEasyTransfer transfer)
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        if (transfer is null)
        {
            return CurlMultiCode.BadEasyHandle;
        }

        // Before the added-already check: curl 8.22.0 refuses a handle already in the multi handle with 8 here too.
        if (IsInTransferCallback)
        {
            return CurlMultiCode.RecursiveApiCall;
        }

        return owners.TryAdd(transfer, this) ? AddEntry(transfer) : CurlMultiCode.AddedAlready;
    }

    // Adds a transfer this handle now owns; also the engine's own add of an HTTP/2 pushed transfer, which
    // runs inside a transfer and so skips the recursive-call check.
    private CurlMultiCode AddEntry(IEasyTransfer transfer)
    {
        entries.Add(new MultiEntry(transfer));
        transfersAdded++;
        return UpdateTimer(tellsAgain: true);
    }

    /// <summary>
    /// <c>curl_multi_remove_handle</c>: removes <paramref name="transfer" />, cancelling its perform if it
    /// still runs and dropping its unread message, as libcurl does; a socket the socket callback was told
    /// of is given <see cref="CurlPoll.Remove" /> first, and that call's answer is not checked.
    /// </summary>
    /// <param name="transfer">The transfer to remove.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />, also for a transfer in no multi handle;
    /// <see cref="CurlMultiCode.BadEasyHandle" /> for <see langword="null" /> or one in another multi handle,
    /// and <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed; called from inside one of its
    /// own transfers' callbacks it removes nothing and answers <see cref="CurlMultiCode.RecursiveApiCall" /> (CGR-146).
    /// </returns>
    public CurlMultiCode Remove(IEasyTransfer transfer)
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        if (transfer is null)
        {
            return CurlMultiCode.BadEasyHandle;
        }

        if (!owners.TryGetValue(transfer, out ManagedMultiTransfer? owner))
        {
            return CurlMultiCode.Ok;
        }

        if (owner != this)
        {
            return CurlMultiCode.BadEasyHandle;
        }

        if (IsInTransferCallback)
        {
            return CurlMultiCode.RecursiveApiCall;
        }

        RemoveEntry(entries.Find(candidate => candidate.Transfer == transfer)!);
        return CurlMultiCode.Ok;
    }

}
