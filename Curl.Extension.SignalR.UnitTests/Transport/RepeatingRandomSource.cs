using Curl.WebSocket;

namespace Curl.Extension.SignalR.Transport;

/// <summary>An <see cref="IWebSocketRandomSource"/> that fills every request from the start of the same bytes, repeated.</summary>
internal sealed class RepeatingRandomSource(params byte[] bytes) : IWebSocketRandomSource
{
    public void Fill(Span<byte> destination)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = bytes[index % bytes.Length];
        }
    }
}
