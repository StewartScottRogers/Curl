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
    private void QueueDoneMessage(MultiEntry entry, CurlCode result)
    {
        entry.Done = true;
        entry.SocketWait?.StopGating();
        if (messages.Count == 0 && IsEnabled(CurlMultiNotification.InfoRead))
        {
            pendingNotifications.Add((CurlMultiNotification.InfoRead, entry.Transfer));
        }

        messages.Add(new CurlMultiMessage(CurlMultiMessageType.Done, entry.Transfer, result));
        if (IsEnabled(CurlMultiNotification.EasyDone))
        {
            pendingNotifications.Add((CurlMultiNotification.EasyDone, entry.Transfer));
        }
    }

    private void SendNotifications()
    {
        (CurlMultiNotification Notification, IEasyTransfer Transfer)[] sending = [.. pendingNotifications];
        pendingNotifications.Clear();
        foreach ((CurlMultiNotification notification, IEasyTransfer transfer) in sending)
        {
            Options.Notify?.Invoke(notification, transfer);
        }
    }

    private void ClearForNetworkChange(CurlNetworkChange change)
    {
        if (change.HasFlag(CurlNetworkChange.ClearConnections))
        {
            idleConnectionsClosing = Task.WhenAll(idleConnectionsClosing, closeIdleConnections().AsTask());
        }

        if (change.HasFlag(CurlNetworkChange.ClearDns))
        {
            clearDnsCache();
        }
    }

    /// <summary>
    /// Whether <paramref name="entry" /> holds no socket of its own: under <see cref="CurlPipelining.Multiplex" />
    /// with <c>CURLOPT_PIPEWAIT</c> and not held to HTTP/1, it waits for a running transfer to the same
    /// origin added before it to open the connection it then shares, as libcurl's does (upstream test2407, BL-2750).
    /// </summary>
    private bool WaitsForEarlierConnection(MultiEntry entry, IEnumerable<MultiEntry> earlier)
    {
        HttpVersion version = entry.Transfer.Options.Http.HttpVersion;
        return Options.Pipelining.HasFlag(CurlPipelining.Multiplex)
            && entry.Transfer.Options.Http.PipeWait
            && version is not (HttpVersion.Http10 or HttpVersion.Http11)
            && earlier.Any(other => other.Origin == entry.Origin);
    }

    private static CurlSocket? WaitSocketOf(MultiEntry entry) => entry.SocketRecord?.Socket is { } handle ? new CurlSocket(handle) : null;

    private static TaskCompletionSource NewWakeupSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool HasWorkForPerform(MultiEntry entry) => (!entry.Done && entry.Perform is null && CanStart(entry)) || IsFinishedUnreported(entry);

    private bool IsPending(MultiEntry entry) => !entry.Done && entry.Perform is null && !CanStart(entry);

    /// <summary>
    /// Whether <paramref name="candidate" /> may start now: it joins a multiplexed connection to its origin
    /// with a stream free, or the connection it opens stays within both connection limits.
    /// </summary>
    private bool CanStart(MultiEntry candidate)
    {
        List<MultiEntry> running = entries.FindAll(entry => entry.Perform is not null && !entry.Done);
        string origin = OriginOf(candidate.Transfer);
        if (IsMultiplexed(candidate.Transfer, origin) && running.Count(entry => entry.Origin == origin && entry.Multiplexed) % ConcurrentStreams() != 0)
        {
            return true;
        }

        return IsWithin(Options.MaxHostConnections, ConnectionsUsed(running.FindAll(entry => entry.Origin == origin)))
            && IsWithin(Options.MaxTotalConnections, ConnectionsUsed(running));
    }

    private static bool IsWithin(long limit, long used) => limit <= 0 || used < limit;

    private long ConnectionsUsed(List<MultiEntry> running)
    {
        long streams = ConcurrentStreams();
        return running.GroupBy(entry => (entry.Origin, entry.Multiplexed))
            .Sum(group => group.Key.Multiplexed ? (group.Count() + streams - 1) / streams : group.Count());
    }

    private long ConcurrentStreams() =>
        Options.MaxConcurrentStreams is < 1 or > int.MaxValue ? DefaultConcurrentStreams : Options.MaxConcurrentStreams;

    /// <summary>
    /// Whether a transfer to <paramref name="origin" /> shares its connection under
    /// <see cref="CurlPipelining.Multiplex" />: an <c>https</c> URL not held to HTTP/1 (ALPN offers h2 by
    /// default) or HTTP/2 with prior knowledge.
    /// </summary>
    private bool IsMultiplexed(IEasyTransfer transfer, string origin)
    {
        HttpVersion version = transfer.Options.Http.HttpVersion;
        return Options.Pipelining.HasFlag(CurlPipelining.Multiplex)
            && version is not (HttpVersion.Http10 or HttpVersion.Http11)
            && (origin.StartsWith("https://", StringComparison.Ordinal) || version == HttpVersion.Http2PriorKnowledge);
    }

    /// <summary>The scheme, host and port of the transfer's URL, or the URL text as it is when it does not parse.</summary>
    private static string OriginOf(IEasyTransfer transfer)
    {
        string? url = transfer.Options.Network.Url;
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? $"{uri.Scheme}://{uri.IdnHost}:{uri.Port}" : url ?? string.Empty;
    }

    private static bool IsFinishedUnreported(MultiEntry entry) => !entry.Done && entry.Perform is { IsCompleted: true };

    private void RemoveEntry(MultiEntry entry)
    {
        if (entry.Socket is { } socket)
        {
            Options.Socket?.Invoke(entry.Transfer, socket, CurlPoll.Remove, entry.SocketState);
        }

        entry.SocketWait?.StopGating();
        entry.Cancellation.Cancel();
        entries.Remove(entry);
        messages.RemoveAll(message => message.Transfer == entry.Transfer);
        owners.TryRemove(entry.Transfer, out _);
    }

}
