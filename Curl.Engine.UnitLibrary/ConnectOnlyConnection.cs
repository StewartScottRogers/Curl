using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Multi;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The raw connection a <see cref="ManagedEasyTransfer" /> perform with <c>CURLOPT_CONNECT_ONLY</c> 1 leaves open,
/// which <c>curl_easy_recv</c> and <c>curl_easy_send</c> move bytes on without blocking (ADR-0608). A receive starts one
/// read on the <see cref="IConnection" /> and answers <see cref="CurlCode.Again" /> until it completes, then hands its
/// bytes over, keeping what does not fit for the next receive; a send hands the bytes to one write and answers them
/// all sent, and <see cref="CurlCode.Again" /> while that write is still running. With no connection open, or while
/// the handle's options no longer ask for connect-only, both answer <see cref="CurlCode.UnsupportedProtocol" />, as
/// libcurl's <c>easy_connection</c> does.
/// </summary>
/// <param name="isConnectOnly">Whether the handle's options set <c>CURLOPT_CONNECT_ONLY</c> now.</param>
internal sealed class ConnectOnlyConnection(Func<bool> isConnectOnly) : IConnectOnlyConnection
{
    /// <summary>The schemes whose connection starts with a TLS handshake, as libcurl's <c>PROTOPT_SSL</c> handlers.</summary>
    private static readonly HashSet<string> TlsSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https", "ftps", "gophers", "imaps", "ldaps", "mqtts", "pop3s", "smbs", "smtps", "wss",
    };

    /// <summary>The read size a wait for <see cref="CurlWaitEvents.PollIn" /> starts a read with, before any receive names one.</summary>
    private const int WaitReadLength = 16 * 1024;

    /// <summary>The last socket number <see cref="OpenAsync" /> gave (<see cref="FirstSocket" /> before any), shared by every handle in the process.</summary>
    private static long nextSocket = FirstSocket;

    private IConnection? connection;

    private Task<int>? pendingRead;

    private byte[] readBuffer = [];

    private ReadOnlyMemory<byte> unread;

    private Task? pendingSend;

    /// <summary>Whether a connection to <paramref name="scheme" /> starts with a TLS handshake.</summary>
    /// <param name="scheme">The URL's scheme.</param>
    /// <returns><see langword="true" /> for a TLS scheme such as <c>https</c>.</returns>
    internal static bool UsesTls(string scheme) => TlsSchemes.Contains(scheme);

    /// <summary>
    /// Gets the socket number the numbering starts from; the first connection is given <c>FirstSocket + 1</c>, the
    /// next <c>FirstSocket + 2</c> and so on. Only a connection with no operating system socket (an in-memory one)
    /// is numbered, each opened one with a number of the engine's own, above any descriptor a process is likely to
    /// hold and apart from <see cref="TransferSocketWait.FirstEngineSocket" />'s numbers.
    /// </summary>
    internal const long FirstSocket = 0x6000_0000;

    /// <summary>
    /// Gets the socket number of the connection open now, for <c>CURLINFO_ACTIVESOCKET</c> and
    /// <c>CURLINFO_LASTSOCKET</c>, or <see cref="CurlSocket.Bad" /> while none is open or the handle's options no
    /// longer ask for connect-only (ADR-0549, BL-2633).
    /// </summary>
    internal CurlSocket Socket { get; private set; } = CurlSocket.Bad;

    /// <summary>
    /// Gets or sets whether the handle's perform is running, so a receive or send made now comes from one of its
    /// callbacks and answers <see cref="CurlCode.RecursiveApiCall" />, as libcurl's <c>Curl_is_in_callback</c> check does.
    /// </summary>
    internal bool PerformRunning { get; set; }

    /// <summary>Gets the socket number of the connection open now while the options ask for connect-only, else <see cref="CurlSocket.Bad" />.</summary>
    internal CurlSocket LiveSocket => LiveConnection() is null ? CurlSocket.Bad : Socket;

    /// <summary>
    /// Closes the connection open now, if any, and keeps <paramref name="opened" /> in its place under its operating
    /// system socket (<see cref="IConnection.SocketHandle" />), as libcurl's <c>CURLINFO_ACTIVESOCKET</c> answers its
    /// own socket (CGR-129, ADR-0842), else under a new socket number.
    /// </summary>
    /// <param name="opened">The connection the connect-only perform made.</param>
    /// <returns>A task that completes once the earlier connection is closed.</returns>
    internal async ValueTask OpenAsync(IConnection opened)
    {
        await CloseAsync().ConfigureAwait(false);
        connection = opened;
        Socket = new CurlSocket(opened.SocketHandle ?? (nint)Interlocked.Increment(ref nextSocket));
    }

    /// <summary>
    /// The events of <paramref name="events" /> the connection is ready for now, as <c>poll</c> answers for
    /// libcurl's socket in <c>curl_multi_wait</c>'s extra sockets: <see cref="CurlWaitEvents.PollOut" /> while no
    /// send is still running, and <see cref="CurlWaitEvents.PollIn" /> once bytes, or the peer's close, wait to be
    /// received; a wait for <see cref="CurlWaitEvents.PollIn" /> starts the read a receive would.
    /// </summary>
    /// <param name="events">The events the caller waits for.</param>
    /// <returns>The events that happened, or <see cref="CurlWaitEvents.None" /> while no connection is live.</returns>
    internal CurlWaitEvents ReadyEvents(CurlWaitEvents events)
    {
        return LiveConnection() is { } live ? WritableEvents(events) | ReadableEvents(live, events) : CurlWaitEvents.None;
    }

    private CurlWaitEvents WritableEvents(CurlWaitEvents events) =>
        events.HasFlag(CurlWaitEvents.PollOut) && pendingSend is not { IsCompleted: false } ? CurlWaitEvents.PollOut : CurlWaitEvents.None;

    private CurlWaitEvents ReadableEvents(IConnection live, CurlWaitEvents events) =>
        events.HasFlag(CurlWaitEvents.PollIn) && IsReadable(live) ? CurlWaitEvents.PollIn : CurlWaitEvents.None;

    private bool IsReadable(IConnection live) => !unread.IsEmpty || (pendingRead ??= StartRead(live, WaitReadLength)).IsCompleted;

    /// <summary>Closes the connection open now, if any, forgetting any bytes read and not yet received.</summary>
    /// <returns>A task that completes once the connection is closed.</returns>
    internal async ValueTask CloseAsync()
    {
        IConnection? open = connection;
        connection = null;
        Socket = CurlSocket.Bad;
        pendingRead = null;
        pendingSend = null;
        unread = default;
        if (open is not null)
        {
            await open.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<CurlConnectOnlyResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (PerformRunning)
        {
            return new CurlConnectOnlyResult(CurlCode.RecursiveApiCall, 0);
        }

        if (LiveConnection() is not { } live)
        {
            return new CurlConnectOnlyResult(CurlCode.UnsupportedProtocol, 0);
        }

        if (unread.IsEmpty)
        {
            pendingRead ??= StartRead(live, buffer.Length);
            if (!pendingRead.IsCompleted)
            {
                return new CurlConnectOnlyResult(CurlCode.Again, 0);
            }

            Task<int> read = pendingRead;
            pendingRead = null;
            try
            {
                unread = readBuffer.AsMemory(0, await read.ConfigureAwait(false));
            }
            catch (IOException)
            {
                return new CurlConnectOnlyResult(CurlCode.RecvError, 0);
            }
        }

        int count = Math.Min(unread.Length, buffer.Length);
        unread[..count].CopyTo(buffer);
        unread = unread[count..];
        return new CurlConnectOnlyResult(CurlCode.Ok, count);
    }

    /// <inheritdoc />
    public async ValueTask<CurlConnectOnlyResult> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (PerformRunning)
        {
            return new CurlConnectOnlyResult(CurlCode.RecursiveApiCall, 0);
        }

        if (LiveConnection() is not { } live)
        {
            return new CurlConnectOnlyResult(CurlCode.UnsupportedProtocol, 0);
        }

        if (pendingSend is { IsCompleted: false })
        {
            return new CurlConnectOnlyResult(CurlCode.Again, 0);
        }

        if (!await TakeFinishedSendAsync().ConfigureAwait(false))
        {
            return new CurlConnectOnlyResult(CurlCode.SendError, 0);
        }

        pendingSend = WriteAndFlushAsync(live, buffer.ToArray());
        return await TakeFinishedSendAsync().ConfigureAwait(false)
            ? new CurlConnectOnlyResult(CurlCode.Ok, buffer.Length)
            : new CurlConnectOnlyResult(CurlCode.SendError, 0);
    }

    private static async Task WriteAndFlushAsync(IConnection live, byte[] bytes)
    {
        await live.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        await live.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private IConnection? LiveConnection() => isConnectOnly() ? connection : null;

    private Task<int> StartRead(IConnection live, int length)
    {
        readBuffer = new byte[Math.Max(length, 1)];
        return live.ReadAsync(readBuffer, CancellationToken.None).AsTask();
    }

    /// <summary>
    /// Forgets the send that has completed, if any, answering whether it succeeded; a send still running stays.
    /// </summary>
    /// <returns><see langword="false" /> when the completed send failed with an <see cref="IOException" />.</returns>
    private async ValueTask<bool> TakeFinishedSendAsync()
    {
        if (pendingSend is not { IsCompleted: true } finished)
        {
            return true;
        }

        pendingSend = null;
        try
        {
            await finished.ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
