using System.Security.Cryptography;

namespace Curl.Protocol.Ws;

/// <summary>
/// Fills WebSocket keys and masks from <see cref="RandomNumberGenerator" />, the operating
/// system's cryptographic random source.
/// </summary>
public sealed class SystemWebSocketRandomSource : IWebSocketRandomSource
{
    /// <inheritdoc />
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
