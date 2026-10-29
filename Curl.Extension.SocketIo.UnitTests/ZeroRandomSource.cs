using Curl.WebSocket;

namespace Curl.Extension.SocketIo;

/// <summary>Randomness of zeros: the key is 16 zero bytes and every mask is zero.</summary>
internal sealed class ZeroRandomSource : IWebSocketRandomSource
{
    public const string Key = "AAAAAAAAAAAAAAAAAAAAAA==";

    public void Fill(Span<byte> destination) => destination.Clear();
}
