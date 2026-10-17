namespace Curl.Api;

/// <summary>
/// What <see cref="CurlWebSocket.ReadMessagesAsync"/> returns: each enumeration receives messages
/// with <see cref="CurlWebSocket.ReceiveAsync"/> until it has given the peer's close frame,
/// cancelled by either the token given to <see cref="CurlWebSocket.ReadMessagesAsync"/> or the
/// one given to <see cref="GetAsyncEnumerator"/>.
/// </summary>
/// <param name="socket">The WebSocket the messages come from.</param>
/// <param name="readToken">The token given to <see cref="CurlWebSocket.ReadMessagesAsync"/>.</param>
internal sealed class CurlWebSocketMessageReader(CurlWebSocket socket, CancellationToken readToken) : IAsyncEnumerable<CurlWebSocketMessage>
{
    /// <inheritdoc/>
    public IAsyncEnumerator<CurlWebSocketMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new Enumerator(socket, CancellationTokenSource.CreateLinkedTokenSource(readToken, cancellationToken));

    /// <summary>One enumeration of the messages.</summary>
    /// <param name="socket">The WebSocket the messages come from.</param>
    /// <param name="cancellation">Both tokens linked; disposed with the enumerator.</param>
    private sealed class Enumerator(CurlWebSocket socket, CancellationTokenSource cancellation) : IAsyncEnumerator<CurlWebSocketMessage>
    {
        private CurlWebSocketMessage? _current;

        /// <inheritdoc/>
        public CurlWebSocketMessage Current => _current ?? throw new InvalidOperationException("No message has been received yet.");

        /// <inheritdoc/>
        public async ValueTask<bool> MoveNextAsync()
        {
            if (_current?.Type == CurlWebSocketMessageType.Close)
            {
                return false;
            }

            _current = await socket.ReceiveAsync(cancellation.Token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            cancellation.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
