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

    /// <summary>Initializes a new instance of the <see cref="McpServer" /> class.</summary>
    /// <param name="transport">The transport the server reads messages from and writes responses to.</param>
    /// <param name="serverInfo">The name and version <c>initialize</c> answers with.</param>
    /// <param name="handlers">The handlers of every other method, e.g. <c>tools/list</c>; a later handler for a method replaces an earlier one.</param>
    public McpServer(IMcpTransport transport, McpServerInfo serverInfo, IEnumerable<IMcpMethodHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(serverInfo);
        ArgumentNullException.ThrowIfNull(handlers);
        _transport = transport;
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
    /// cancelled, then cancels the requests still in flight and returns once they have finished.
    /// </summary>
    /// <param name="cancellationToken">The host's token; cancelling it ends the loop and every request.</param>
    /// <returns>A task that completes when the loop and every request it started have ended; it rethrows a failure of the transport's read once they have.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        List<Task> running = [];
        Task reading = ReadMessagesAsync(running, cancellationToken);
        await reading.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _tracker.CancelAll();
        await Task.WhenAll(running).ConfigureAwait(false);
        await reading.ConfigureAwait(false);
    }

    private async Task ReadMessagesAsync(List<Task> running, CancellationToken cancellationToken)
    {
        try
        {
            while (await _transport.ReadMessageAsync(cancellationToken).ConfigureAwait(false) is { } message)
            {
                running.RemoveAll(task => task.IsCompleted);
                running.Add(DispatchAsync(message, cancellationToken));
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
        catch (Exception)
        {
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
        catch (Exception)
        {
            // A notification has no response to carry an error; a failing one must not end the server.
        }
    }

    // The SDK 2.2.0 server answered nothing to malformed JSON, a non-object or a batch (the golden
    // files of BL-3374), so an invalid message is answered only when it carries an id to answer to.
    private Task AnswerInvalidMessageAsync(JsonRpcInvalidMessage message) =>
        message.Id is null ? Task.CompletedTask : WriteAsync(_writer.WriteError(message));

    private async Task WriteAsync(byte[] response)
    {
        await _writeLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await _transport.WriteMessageAsync(response, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
