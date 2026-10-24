using System.Text;

namespace Curl.Conformance.Differential.Protocols.Core;

/// <summary>Writes a protocol server's replies the way proto_servers.py's <c>Line.send</c> does: the bytes, flushed at once.</summary>
public sealed class ProtocolLineWriter(Stream stream)
{
    /// <summary>Writes <paramref name="text"/> as UTF-8 and flushes it.</summary>
    public Task SendAsync(string text, CancellationToken cancellationToken) => SendAsync(Encoding.UTF8.GetBytes(text), cancellationToken);

    /// <summary>Writes <paramref name="bytes"/> and flushes them.</summary>
    public async Task SendAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
