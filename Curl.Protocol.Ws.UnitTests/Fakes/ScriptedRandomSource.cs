namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// An <see cref="IWebSocketRandomSource" /> that fills every request with the same bytes, so a
/// frame's mask can be set to one curl 8.21.0 was measured using and its bytes compared whole.
/// </summary>
/// <param name="bytes">The bytes every request is filled with, from the start.</param>
public sealed class ScriptedRandomSource(params byte[] bytes) : IWebSocketRandomSource
{
    /// <inheritdoc />
    public void Fill(Span<byte> destination) => bytes.AsSpan(0, destination.Length).CopyTo(destination);
}
