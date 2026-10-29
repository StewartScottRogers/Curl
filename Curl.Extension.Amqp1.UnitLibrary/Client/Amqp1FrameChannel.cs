using Curl.Extension.Amqp1.Frames;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Amqp1.Client;

/// <summary>
/// Sends and receives AMQP 1.0 protocol headers and whole frames over an <see cref="IConnection"/>, plain or TLS.
/// A fault in what the peer sends raises <see cref="Amqp1MalformedEncodingException"/>.
/// </summary>
internal sealed class Amqp1FrameChannel(IConnection transport, CancellationToken cancellationToken)
{
    private const int ReadSize = 4096;

    private byte[] buffer = new byte[ReadSize];

    private int filled;

    /// <summary>Writes the protocol header for <paramref name="protocolId"/>.</summary>
    internal ValueTask SendHeaderAsync(byte protocolId) => WriteAsync(Amqp1ProtocolHeader.Write(protocolId));

    /// <summary>Reads the peer's protocol header and requires it to be the one for <paramref name="protocolId"/>.</summary>
    internal async ValueTask ReceiveHeaderAsync(byte protocolId)
    {
        while (filled < Amqp1ProtocolHeader.Length)
        {
            await ReadMoreAsync().ConfigureAwait(false);
        }

        Amqp1ProtocolHeader.RequireProtocol(buffer.AsSpan(0, Amqp1ProtocolHeader.Length), protocolId);
        Consume(Amqp1ProtocolHeader.Length);
    }

    /// <summary>Writes <paramref name="frame"/>.</summary>
    internal ValueTask SendAsync(Amqp1Frame frame) => WriteAsync(Amqp1FrameCodec.Write(frame));

    /// <summary>Reads the next frame that is not a heartbeat and gives its performative, or SASL frame body, and payload.</summary>
    internal async ValueTask<(Amqp1Composite Performative, ReadOnlyMemory<byte> Payload)> ReceiveAsync()
    {
        while (true)
        {
            if (!Amqp1FrameCodec.TryRead(buffer.AsMemory(0, filled), out var frame, out var consumed))
            {
                await ReadMoreAsync().ConfigureAwait(false);
                continue;
            }

            var body = frame!.IsHeartbeat ? default : frame.ReadBody();
            byte[] payload = body.Payload.ToArray();
            Consume(consumed);
            if (body.Performative is { } performative)
            {
                return (performative, payload);
            }
        }
    }

    private void Consume(int length)
    {
        buffer.AsSpan(length, filled - length).CopyTo(buffer);
        filled -= length;
    }

    private async ValueTask WriteAsync(byte[] bytes)
    {
        try
        {
            await transport.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Amqp1ClientException(CurlExitCode.SendError, Amqp1ClientMessages.SendFailed);
        }
    }

    private async ValueTask ReadMoreAsync()
    {
        if (buffer.Length - filled < ReadSize)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
        }

        int read;
        try
        {
            read = await transport.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Amqp1ClientException(CurlExitCode.RecvError, Amqp1ClientMessages.ReceiveFailed);
        }

        filled += read > 0 ? read : throw new Amqp1ClientException(CurlExitCode.RecvError, Amqp1ClientMessages.ConnectionDisconnected);
    }
}
