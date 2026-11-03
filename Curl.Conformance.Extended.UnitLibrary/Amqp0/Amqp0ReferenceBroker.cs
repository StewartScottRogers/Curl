using System.Buffers.Binary;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Amqp0;

/// <summary>
/// A reference AMQP 0-9-1 broker written from the specification (2008-11-13): it reads the client's protocol
/// header (4.2.2), sends connection.start, then reads whole frames (4.2.3) and answers each method frame through its
/// <see cref="Amqp0MethodTable"/>, skipping content and heartbeat frames. It keeps every frame the client sent.
/// </summary>
/// <param name="settings">What connection.start offers and connection.tune proposes.</param>
/// <param name="methods">The answer to each client method.</param>
public sealed class Amqp0ReferenceBroker(Amqp0BrokerSettings settings, Amqp0MethodTable methods) : IReferenceServer
{
    private const int HeaderSize = 7;

    private readonly List<Amqp0BrokerFrame> clientFrames = [];

    private ReferenceConnection? connection;

    /// <summary>A broker with the default settings and <see cref="Amqp0MethodTable.Standard"/>.</summary>
    public Amqp0ReferenceBroker()
        : this(new Amqp0BrokerSettings(), Amqp0MethodTable.Standard)
    {
    }

    /// <summary>What connection.start offers and connection.tune proposes.</summary>
    public Amqp0BrokerSettings Settings => settings;

    /// <summary>The eight bytes the client opened with.</summary>
    public byte[] ClientProtocolHeader { get; private set; } = [];

    /// <summary>Every frame the client sent, in order.</summary>
    public IReadOnlyList<Amqp0BrokerFrame> ClientFrames => clientFrames;

    /// <summary>The method frames the client sent, as "class.method" (such as "10.11").</summary>
    public IReadOnlyList<string> ClientMethods =>
        [.. clientFrames.Where(frame => frame.Type == Amqp0BrokerFrame.MethodType).Select(frame => $"{frame.ClassId}.{frame.MethodId}")];

    /// <summary>The first method frame the client sent of the given class and method.</summary>
    public Amqp0BrokerFrame ClientMethod(ushort classId, ushort methodId) =>
        clientFrames.First(frame => frame.Type == Amqp0BrokerFrame.MethodType && frame.ClassId == classId && frame.MethodId == methodId);

    /// <inheritdoc/>
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        this.connection = connection;
        ClientProtocolHeader = await ReadUpToAsync(8, cancellationToken).ConfigureAwait(false);
        if (settings.ProtocolHeaderReply is { } reply)
        {
            await SendAsync(reply, cancellationToken).ConfigureAwait(false);
            return;
        }

        Amqp0ArgumentWriter start = new Amqp0ArgumentWriter().Octet(0).Octet(9)
            .Table(("product", "Amqp0ReferenceBroker"), ("version", "0-9-1"))
            .LongString(System.Text.Encoding.UTF8.GetBytes(settings.Mechanisms))
            .LongString(System.Text.Encoding.UTF8.GetBytes(settings.Locales));
        bool open = await ReplyAsync(0, 10, 10, start, cancellationToken).ConfigureAwait(false);
        while (open && await ReadFrameAsync(cancellationToken).ConfigureAwait(false) is { } frame)
        {
            open = frame.Type != Amqp0BrokerFrame.MethodType || await methods.HandlerOf(frame)(this, frame, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Sends a method frame; returns true so a handler can end with it and keep the connection open.</summary>
    public async ValueTask<bool> ReplyAsync(ushort channel, ushort classId, ushort methodId, Amqp0ArgumentWriter arguments, CancellationToken cancellationToken)
    {
        await SendAsync(Amqp0BrokerFrame.EncodeMethod(channel, classId, methodId, arguments), cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Sends bytes as they are, such as a malformed frame.</summary>
    public async ValueTask<bool> SendAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await connection!.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async ValueTask<Amqp0BrokerFrame?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        byte[] header = await ReadUpToAsync(HeaderSize, cancellationToken).ConfigureAwait(false);
        if (header.Length < HeaderSize)
        {
            return null;
        }

        int size = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(3)));
        byte[] rest = await ReadUpToAsync(size + 1, cancellationToken).ConfigureAwait(false);
        Amqp0BrokerFrame frame = new(header[0], BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1)), rest[..size], rest[size]);
        clientFrames.Add(frame);
        return frame;
    }

    private async ValueTask<byte[]> ReadUpToAsync(int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int filled = 0;
        int read = 1;
        while (filled < count && read > 0)
        {
            read = await connection!.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            filled += read;
        }

        return buffer[..filled];
    }
}
