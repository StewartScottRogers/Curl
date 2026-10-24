using Curl.Mcp.Protocol.JsonRpc;
using Curl.Mcp.Protocol.Transport;

namespace Curl.Mcp.Protocol.Server;

/// <summary>
/// Runs the MCP server loop over an <see cref="IMcpTransport" />: reads each message, runs each request
/// on its own task through the <see cref="IMcpMethodHandler" /> registered for its method and writes
/// its result or JSON-RPC error, and answers the lifecycle methods itself (<c>initialize</c>,
/// <c>ping</c>, <c>notifications/initialized</c>, <c>notifications/cancelled</c>).
/// </summary>
public sealed class McpServer
{
    private readonly IMcpTransport _transport;

    private readonly Dictionary<string, IMcpMethodHandler> _handlers = new(StringComparer.Ordinal);

    private readonly McpRequestTracker _tracker = new();

    private readonly JsonRpcResponseWriter _writer = new();

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private readonly CancellationTokenSource _writeFailed = new();

    private readonly Action<string> _logFailure;

    private Exception? _writeFailure;

    /// <summary>Initializes a new instance of the <see cref="McpServer" /> class.</summary>
    /// <param name="transport">The transport the server reads messages from and writes responses to.</param>
    /// <param name="serverInfo">The name and version <c>initialize</c> answers with.</param>
    /// <param name="handlers">The handlers of every other method, e.g. <c>tools/list</c>; a later handler for a method replaces an earlier one.</param>
    /// <param name="logFailure">Receives one line for each failure the server hides from the client: a handler that threw and a message it discarded.</param>
    public McpServer(IMcpTransport transport, McpServerInfo serverInfo, IEnumerable<IMcpMethodHandler> handlers, Action<string> logFailure)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(serverInfo);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(logFailure);
        _transport = transport;
        _logFailure = logFailure;
        IMcpMethodHandler[] methodHandlers = [.. handlers];
        Register(new McpInitializeHandler(serverInfo, methodHandlers));
        Register(new McpPingHandler());
        Register(new McpInitializedNotificationHandler());
        Register(new McpCancelledNotificationHandler(_tracker));
        foreach (IMcpMethodHandler handler in methodHandlers)
        {
            Register(handler);
        }
    }

    /// <summary>
    /// Reads and answers messages until the transport ends or <paramref name="cancellationToken" /> is
    /// cancelled, then cancels the requests still in flight and returns once they have finished; a read blocked past the cancellation is abandoned.
    /// </summary>
    /// <param name="cancellationToken">The host's token; cancelling it ends the loop and every request.</param>
    /// <returns>A task that completes when the loop and every request it started have ended; it rethrows a failure of the transport's read once they have; a failed response write stops the loop and ends it with an <see cref="McpResponseWriteException" />.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        List<Task> running = [];
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _writeFailed.Token);
        Task reading = ReadMessagesAsync(running, stop.Token);
        await reading.WaitAsync(stop.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _tracker.CancelAll();
        await Task.WhenAll(SnapshotOf(running)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_writeFailure is { } writeFailure)
        {
            throw new McpResponseWriteException(writeFailure);
        }

        if (reading.IsCompleted)
        {
            await reading.ConfigureAwait(false);
        }

        // Otherwise the host cancelled while a read that ignores its token was blocked: that read is
        // abandoned and dies with the process.
    }

    private static Task[] SnapshotOf(List<Task> running)
    {
        lock (running)
        {
            return [.. running];
        }
    }

    private async Task ReadMessagesAsync(List<Task> running, CancellationToken cancellationToken)
    {
        try
        {
            while (await _transport.ReadMessageAsync(cancellationToken).ConfigureAwait(false) is { } message)
            {
                Task dispatched = DispatchAsync(message, cancellationToken);
                lock (running)
                {
                    running.RemoveAll(task => task.IsCompleted);
                    running.Add(dispatched);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A read cancelled by the host or the transport ends the loop as the transport's end does.
        }
    }

    private void Register(IMcpMethodHandler handler) => _handlers[handler.Method] = handler;

    private Task DispatchAsync(JsonRpcIncoming message, CancellationToken cancellationToken) => message switch
    {
        JsonRpcRequest request => StartRequest(request, cancellationToken),
        JsonRpcNotification notification => HandleNotificationAsync(notification, cancellationToken),
        _ => AnswerInvalidMessageAsync((JsonRpcInvalidMessage)message),
    };

    private Task StartRequest(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(request.Method, out IMcpMethodHandler? handler))
        {
            return WriteAsync(_writer.WriteError(request.Id, new JsonRpcError(JsonRpcErrorCodes.MethodNotFound, $"Method '{request.Method}' is not available.", null)));
        }

        CancellationTokenSource source = _tracker.Start(request.Id, cancellationToken);
        return Task.Run(() => RunRequestAsync(request, handler, source), CancellationToken.None);
    }

    private async Task RunRequestAsync(JsonRpcRequest request, IMcpMethodHandler handler, CancellationTokenSource source)
    {
        byte[]? response;
        try
        {
            response = _writer.WriteSuccess(request.Id, await handler.HandleAsync(request.Params, source.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            response = null;
        }
        catch (McpProtocolException exception)
        {
            response = _writer.WriteError(request.Id, new JsonRpcError(exception.Code, exception.Message, null));
        }
        catch (Exception exception)
        {
            _logFailure($"The handler of request '{request.Method}' threw {exception.GetType().FullName}: {exception.Message}");
            response = _writer.WriteError(request.Id, new JsonRpcError(JsonRpcErrorCodes.InternalError, "An error occurred.", null));
        }
        finally
        {
            _tracker.Finish(request.Id, source);
            source.Dispose();
        }

        if (response is not null)
        {
            await WriteAsync(response).ConfigureAwait(false);
        }
    }

    private async Task HandleNotificationAsync(JsonRpcNotification notification, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(notification.Method, out IMcpMethodHandler? handler))
        {
            return;
        }

        try
        {
            await handler.HandleAsync(notification.Params, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // A notification has no response to carry an error; a failing one must not end the server.
            _logFailure($"The handler of notification '{notification.Method}' threw {exception.GetType().FullName}: {exception.Message}");
        }
    }

    // The SDK 2.2.0 server answered nothing to malformed JSON, a non-object or a batch (the golden
    // files of BL-3374), so an invalid message is answered only when it carries an id to answer to;
    // one it drops is logged, so a client that hangs on it leaves a trace.
    private Task AnswerInvalidMessageAsync(JsonRpcInvalidMessage message)
    {
        if (message.Id is not null)
        {
            return WriteAsync(_writer.WriteError(message));
        }

        _logFailure($"Discarded a message without answering it: {message.Error.Message} (JSON-RPC error {message.Error.Code}).");
        return Task.CompletedTask;
    }

    private async Task WriteAsync(byte[] response)
    {
        await _writeLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await _transport.WriteMessageAsync(response, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _writeFailure, exception, null);
            _writeFailed.Cancel();
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
