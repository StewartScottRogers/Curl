using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

namespace Curl.Engine;

/// <summary>
/// The managed <see cref="IMultiTransfer" /> (ADR-0506, ADR-0594): the easy transfers added to it run at
/// once, each its own <see cref="IEasyTransfer.PerformAsync" /> over the engine's shared connections.
/// <see cref="PerformAsync" /> starts every transfer added since the last call and queues a
/// <see cref="CurlMultiMessageType.Done" /> message, with the transfer's <see cref="CurlCode" />, for each
/// one that has finished; <see cref="TryReadInfo" /> takes them in the order they were queued.
/// <see cref="Remove" /> stops a running transfer and drops its unread message, and
/// <see cref="DisposeAsync" /> removes every transfer. <see cref="GetTimeout" />, <see cref="WaitAsync" />,
/// <see cref="PollAsync" /> and <see cref="Wakeup" /> run on the engine's <see cref="TimeProvider" />
/// (ADR-0597). <see cref="SocketActionAsync" /> moves the transfers on as <see cref="PerformAsync" /> does, and
/// both tell <see cref="MultiOptions.Socket" /> and <see cref="MultiOptions.Timer" /> what to wait for: a
/// <see cref="ManagedEasyTransfer" />, or a handle whose <see cref="IEasyTransfer.PerformingTransfer" /> is one (ADR-0986),
/// started while the socket callback is set runs on a
/// <see cref="TransferSocketWait" />, its real socket told with <see cref="CurlPoll.Out" /> or
/// <see cref="CurlPoll.In" /> and the timer with its earliest deadline (ADR-0973), and one started without it records
/// its real socket on a <see cref="TransferSocketWait" /> that never gates its I/O (ADR-0982); any other running transfer has
/// no socket the handle knows, so the application is told none for it, and while one runs the timer is 0, as
/// libcurl's <c>Curl_expire(data, 0)</c> for a transfer that can run on (ADR-0998). <see cref="GetHandles" />,
/// <see cref="GetWaitSockets" />, <see cref="GetInfo" />, the notifications and
/// <c>CURLMOPT_NETWORK_CHANGED</c> follow curl 8.22.0 (ADR-0604). A transfer that would open a connection
/// past <see cref="MultiOptions.MaxHostConnections" /> or <see cref="MultiOptions.MaxTotalConnections" />
/// waits, pending, until a running one finishes; under <see cref="CurlPipelining.Multiplex" /> transfers to
/// one origin that can multiplex share a connection, <see cref="MultiOptions.MaxConcurrentStreams" /> at a
/// time, and <see cref="OfferPush" /> answers a server push through <see cref="MultiOptions.Push" /> (ADR-0605).
/// </summary>
/// <param name="owners">
/// The multi handle each of the engine's transfers is in, shared by all its multi handles, so a
/// transfer is in one at a time (<see cref="CurlMultiCode.AddedAlready" />).
/// </param>
/// <param name="timeProvider">The clock the timeout and the waits are measured on.</param>
/// <param name="socketPoller">Checks the extra sockets a wait or poll is given.</param>
/// <param name="closeIdleConnections">
/// Closes the handle's idle connections, for <see cref="CurlNetworkChange.ClearConnections" /> and once every
/// perform has ended in <see cref="DisposeAsync" /> (<see cref="ConnectionCache.CloseIdleAsync" />).
/// </param>
/// <param name="closeIdleConnectionsBeyond">
/// Closes the engine's oldest idle connections beyond a count, after each transfer ends, for
/// <see cref="MultiOptions.MaxConnects" /> (<see cref="ConnectionCache.CloseIdleBeyondAsync" />).
/// </param>
/// <param name="clearDnsCache">Empties the engine's DNS cache, for <see cref="CurlNetworkChange.ClearDns" /> (<see cref="DnsCache.Clear" />).</param>
internal sealed partial class ManagedMultiTransfer(
    ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer> owners,
    TimeProvider timeProvider,
    IWaitSocketPoller socketPoller,
    Func<ValueTask> closeIdleConnections,
    Func<long, ValueTask> closeIdleConnectionsBeyond,
    Action clearDnsCache) : IMultiTransfer
{
    private readonly List<MultiEntry> entries = [];
    private readonly List<CurlMultiMessage> messages = [];
    private readonly List<(CurlMultiNotification Notification, IEasyTransfer Transfer)> pendingNotifications = [];
    private readonly Lock wakeupLock = new();
    private readonly MultiCallbackQueue callbackQueue = new();
    private Task idleConnectionsClosing = Task.CompletedTask;
    private MultiOptions options = new();
    private uint enabledNotifications;
    private long transfersAdded;
    private TaskCompletionSource wakeupSignal = NewWakeupSignal();
    private long reportedTimerMilliseconds = -1;
    private DateTimeOffset reportedTimerDue;
    private bool abortedByCallback;
    private bool disposed;

    // The multi handle whose transfer's perform the current code runs in, or null outside every perform.
    private static readonly AsyncLocal<ManagedMultiTransfer?> PerformingHandle = new();

    /// <inheritdoc />
    public bool IsInTransferCallback => PerformingHandle.Value == this;

    /// <summary>
    /// Gets the socket number <see cref="GetWaitSockets" /> gives after the running transfers' sockets while any
    /// transfer runs: the wakeup descriptor of libcurl's threaded resolver, which <c>curl_version_info</c>'s
    /// <c>AsynchDNS</c> without c-ares says this build has (<see cref="ManagedCurlVersion" />), a number
    /// above any descriptor a process is likely to hold.
    /// </summary>
    internal static nint ResolverWakeupSocket { get; } = 0x3FFF_FFFF;

    /// <summary>
    /// Gets how often a wait with extra sockets checks them again: the managed engine owns no
    /// operating system poll set to add them to, so it asks <see cref="IWaitSocketPoller" /> each interval.
    /// </summary>
    internal static TimeSpan SocketPollInterval { get; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Gets the longest a perform or socket action lets a transfer with a socket wait go on before it answers, when
    /// the transfer neither waits on its socket again nor ends sooner: 200 ms, which tests lengthen so a loaded
    /// machine cannot end an action before the transfer reaches its next wait.
    /// </summary>
    internal TimeSpan SocketWaitSettleLimit { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets the streams per multiplexed connection when <see cref="MultiOptions.MaxConcurrentStreams" /> is
    /// below 1 or past <see cref="int.MaxValue" />, as libcurl's <c>CURLMOPT_MAX_CONCURRENT_STREAMS</c> falls back.
    /// </summary>
    internal static long DefaultConcurrentStreams { get; } = 100;

    /// <summary>
    /// Gets the connections the engine keeps for each transfer in the handle when <see cref="MultiOptions.MaxConnects" />
    /// is 0 or less, as libcurl's <c>CURLMOPT_MAXCONNECTS</c> default does.
    /// </summary>
    internal static long DefaultConnectsPerTransfer { get; } = 4;

    /// <summary>
    /// Gets the step that, before a transfer starts under <see cref="MultiOptions.MaxTotalConnections" />, closes the
    /// handle's oldest idle connections until the one it may open stays within that limit, unless one to its host and
    /// port waits to be reused (<see cref="ConnectionCache.CloseIdleToMakeRoomAsync" />), as libcurl 8.22.0's pool
    /// does (CGR-150); it takes the limit, the host and the port. By default it closes nothing.
    /// </summary>
    internal Func<long, string, int, ValueTask> CloseIdleConnectionsToMakeRoom { get; init; } = static (_, _, _) => ValueTask.CompletedTask;

    /// <summary>
    /// Gets or sets every option of the handle (<c>curl_multi_setopt</c>). <c>CURLMOPT_NETWORK_CHANGED</c>
    /// is an action, not a setting, as in libcurl: a value whose <see cref="MultiOptions.NetworkChanged" />
    /// is not <see cref="CurlNetworkChange.None" /> closes the engine's idle connections
    /// (<see cref="CurlNetworkChange.ClearConnections" />) and empties its DNS cache
    /// (<see cref="CurlNetworkChange.ClearDns" />) at once, and is kept with it set back to
    /// <see cref="CurlNetworkChange.None" />, so a later <see langword="with" /> on it clears nothing again.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null" />.</exception>
    public MultiOptions Options
    {
        get => options;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.NetworkChanged == CurlNetworkChange.None)
            {
                options = value;
                return;
            }

            ClearForNetworkChange(value.NetworkChanged);
            options = value with { NetworkChanged = CurlNetworkChange.None };
        }
    }

}
