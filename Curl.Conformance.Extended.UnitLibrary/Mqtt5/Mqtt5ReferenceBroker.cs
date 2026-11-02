using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Mqtt5;

/// <summary>
/// The MQTT 5 reference broker for <c>mqtt5://</c> conformance (BL-4517), written from OASIS MQTT Version 5.0
/// alone. It requires CONNECT first and once (MQTT-3.1.0-1), checks it (<see cref="Mqtt5ReceivedConnect"/>),
/// answers it with the CONNACK it was given, then answers each later packet through the responder for its
/// type and records every packet. Other case groups add behaviour by passing their own CONNACK and responders,
/// never by changing this file.
/// </summary>
/// <param name="connAck">The CONNACK packet's bytes; a success with no properties when null.</param>
/// <param name="responders">By packet type, the bytes to answer a packet with; a type without one gets no answer.</param>
public sealed class Mqtt5ReferenceBroker(byte[]? connAck = null, IReadOnlyDictionary<int, Func<Mqtt5WirePacket, byte[]>>? responders = null)
    : IReferenceServer
{
    /// <summary>A CONNACK accepting a new session: no Session Present, reason 0x00, no properties.</summary>
    public static readonly byte[] SuccessConnAck = [0x20, 0x03, 0x00, 0x00, 0x00];

    private readonly List<Mqtt5WirePacket> received = [];

    /// <summary>Gets the CONNECT the client sent, once it has.</summary>
    public Mqtt5ReceivedConnect? Connect { get; private set; }

    /// <summary>Gets every packet the client sent, CONNECT first, in order.</summary>
    public IReadOnlyList<Mqtt5WirePacket> ReceivedPackets => received;

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        while (await Mqtt5WirePacket.ReadAsync(connection, cancellationToken).ConfigureAwait(false) is { } packet)
        {
            received.Add(packet);
            byte[] answer = Answer(packet);
            if (answer.Length > 0)
            {
                await connection.WriteAsync(answer, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private byte[] Answer(Mqtt5WirePacket packet)
    {
        bool isConnect = packet.PacketType == Mqtt5WirePacket.Connect;
        if (isConnect != (Connect is null))
        {
            throw new Mqtt5ConformanceViolationException(
                "Mqtt5:MQTT-3.1.0-1",
                isConnect ? "the client sent CONNECT a second time." : $"the client's first packet is type {packet.PacketType}, not CONNECT.");
        }

        return isConnect ? AcceptConnect(packet) : Respond(packet);
    }

    private byte[] AcceptConnect(Mqtt5WirePacket connect)
    {
        Connect = Mqtt5ReceivedConnect.Parse(connect.Body);
        return connAck ?? SuccessConnAck;
    }

    private byte[] Respond(Mqtt5WirePacket packet) =>
        responders is not null && responders.TryGetValue(packet.PacketType, out Func<Mqtt5WirePacket, byte[]>? respond)
            ? respond(packet)
            : [];
}
