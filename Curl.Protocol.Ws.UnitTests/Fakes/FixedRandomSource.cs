namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// An <see cref="IWebSocketRandomSource" /> that fills every request with 0, 1, 2, … so the
/// <c>Sec-WebSocket-Key</c> of every test is <c>AAECAwQFBgcICQoLDA0ODw==</c>.
/// </summary>
public sealed class FixedRandomSource : IWebSocketRandomSource
{
    /// <summary>The key the handler sends when its 16 bytes come from this source.</summary>
    public const string Key = "AAECAwQFBgcICQoLDA0ODw==";

    /// <inheritdoc />
    public void Fill(Span<byte> destination)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = (byte)index;
        }
    }
}
