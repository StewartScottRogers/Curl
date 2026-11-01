using Curl.Extended.Protocol.Amqp0.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp0.Client;

/// <summary>
/// Sends and receives whole AMQP 0-9-1 frames over an <see cref="IConnection"/>, TCP or TLS. A fault in the frames
/// read surfaces as <see cref="Amqp0WireFormatException"/>; a transport failure as <see cref="Amqp0ClientException"/>.
/// </summary>
internal sealed class Amqp0FrameChannel(IConnection transport, CancellationToken cancellationToken)
{
    private const int ReadSize = 4096;

    private byte[] buffer = new byte[ReadSize];

    private int filled;

    /// <summary>The largest frame read, 0 for no limit until connection.tune-ok sets it.</summary>
    internal uint FrameMax { get; set; }

    /// <summary>Writes the protocol header (section 4.2.2) and flushes it.</summary>
    internal ValueTask SendProtocolHeaderAsync() => WriteAsync(Amqp0ProtocolHeader.Bytes.ToArray());

    /// <summary>Writes <paramref name="frames"/> in one write and flushes them.</summary>
    internal ValueTask SendAsync(params IEnumerable<Amqp0Frame> frames) => WriteAsync([.. frames.SelectMany(frame => frame.ToBytes())]);

    /// <summary>
    /// Reads the next whole frame. The peer closing first ends the transfer with a receive error; a server that answers
    /// with a protocol header (it does not speak 0-9-1) is a 501 frame-error.
    /// </summary>
    internal async ValueTask<Amqp0Frame> ReceiveAsync()
    {
        while (true)
        {
            if (TryTakeFrame() is { } frame)
            {
                return frame;
            }

            await ReadMoreAsync().ConfigureAwait(false);
        }
    }

    private Amqp0Frame? TryTakeFrame()
    {
        ThrowIfProtocolHeader();
        if (!Amqp0Frame.TryRead(buffer.AsSpan(0, filled), FrameMax, out Amqp0Frame? frame, out int consumed))
        {
            return null;
        }

        buffer.AsSpan(consumed, filled - consumed).CopyTo(buffer);
        filled -= consumed;
        return frame;
    }

    private void ThrowIfProtocolHeader()
    {
        if (filled >= Amqp0ProtocolHeader.Length && buffer[0] == Amqp0ProtocolHeader.Bytes[0])
        {
            Amqp0ProtocolHeader.Verify(buffer.AsSpan(0, Amqp0ProtocolHeader.Length));
            throw Amqp0WireFormatException.FrameError("The peer answered with protocol header AMQP 0-9-1 instead of connection.start.");
        }
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
            throw new Amqp0ClientException(CurlExitCode.SendError, Amqp0ClientMessages.SendFailed);
        }
    }

    private async ValueTask ReadMoreAsync()
    {
        if (buffer.Length - filled < ReadSize)
        {
            Array.Resize(ref buffer, buffer.Length * 2);
        }

        int read = await ReadTransportAsync().ConfigureAwait(false);
        if (read == 0)
        {
            throw new Amqp0ClientException(CurlExitCode.RecvError, Amqp0ClientMessages.ConnectionDisconnected);
        }

        filled += read;
    }

    private async ValueTask<int> ReadTransportAsync()
    {
        try
        {
            return await transport.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Amqp0ClientException(CurlExitCode.RecvError, Amqp0ClientMessages.ReceiveFailed);
        }
    }
}
