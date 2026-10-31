using Curl.WebSocket;

namespace Curl.Extended.Protocol.Mqtt5.WebSocket;

/// <summary>A fake <see cref="IWebSocketRandomSource"/> that gives its set bytes first and zeros after, so every frame mask is zero.</summary>
internal sealed class ScriptedWebSocketRandomSource(params byte[] first) : IWebSocketRandomSource
{
    /// <summary>RFC 6455 section 1.3's sample nonce, whose key is <c>dGhlIHNhbXBsZSBub25jZQ==</c>.</summary>
    internal static readonly byte[] SampleNonce = "the sample nonce"u8.ToArray();

    /// <summary>The key of <see cref="SampleNonce"/>.</summary>
    internal const string SampleKey = "dGhlIHNhbXBsZSBub25jZQ==";

    /// <summary>The accept RFC 6455 section 1.3 gives for <see cref="SampleKey"/>.</summary>
    internal const string SampleAccept = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";

    private readonly Queue<byte> bytes = new(first);

    public void Fill(Span<byte> destination)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = bytes.TryDequeue(out byte next) ? next : (byte)0;
        }
    }
}
