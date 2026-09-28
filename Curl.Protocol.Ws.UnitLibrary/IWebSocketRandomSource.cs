namespace Curl.Protocol.Ws;

/// <summary>
/// Supplies the random bytes a WebSocket transfer needs: the 16 bytes behind
/// <c>Sec-WebSocket-Key</c> and the 4-byte mask of each frame the client sends (ADR-0128).
/// </summary>
/// <remarks>
/// Injected so tests can pass a source that returns fixed bytes and pin the request and
/// frame bytes exactly; <see cref="SystemWebSocketRandomSource" /> is the production source.
/// </remarks>
public interface IWebSocketRandomSource
{
    /// <summary>Fills <paramref name="destination" /> with random bytes.</summary>
    /// <param name="destination">The bytes to fill.</param>
    void Fill(Span<byte> destination);
}
