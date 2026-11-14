using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Coap;

/// <summary>
/// A reference CoAP server written from RFC 7252: it decodes each datagram curl sends with its own
/// <see cref="CoapReferenceMessage" /> codec (a datagram that breaks the message format fails the run),
/// records it with the time it arrived on the run's clock, and sends back what its <see cref="CoapResponder" /> gives.
/// </summary>
/// <param name="respond">Gives the answer to each datagram.</param>
/// <param name="clock">The run's clock, which stamps each datagram.</param>
public sealed class CoapReferenceServer(CoapResponder respond, TimeProvider clock) : IDatagramReferenceServer
{
    private readonly Lock gate = new();
    private readonly List<CoapReceivedDatagram> received = [];

    /// <summary>Gets the datagrams curl sent, in order.</summary>
    public IReadOnlyList<CoapReceivedDatagram> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram)
    {
        CoapReferenceMessage message = CoapReferenceMessage.Decode(datagram.Span);
        int index;
        lock (gate)
        {
            index = received.Count;
            received.Add(new CoapReceivedDatagram(datagram.ToArray(), message, clock.GetUtcNow()));
        }

        return [.. respond(message, index).Select(answer => new ReadOnlyMemory<byte>(answer))];
    }

    /// <inheritdoc />
    public bool IsFromAnotherEndpoint(ReadOnlyMemory<byte> answer)
    {
        if (!System.Runtime.InteropServices.MemoryMarshal.TryGetArray(answer, out ArraySegment<byte> segment))
        {
            return false;
        }

        return CoapResponses.IsFromAnotherEndpoint(segment.Array!);
    }
}
