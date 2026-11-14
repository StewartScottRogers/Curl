namespace Curl.Conformance.Extended.Coap;

/// <summary>The answers a <see cref="CoapResponder" /> sends, encoded from RFC 7252 sections 3, 4 and 5.2.</summary>
public static class CoapResponses
{
    /// <summary>The 2.05 Content response code.</summary>
    public const byte Content = 0x45;

    /// <summary>Gets a responder that never answers.</summary>
    public static CoapResponder Silence { get; } = static (_, _) => [];

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<byte[], object> FromAnotherEndpointMarks = [];

    /// <summary>Marks a datagram so the server delivers it from an endpoint other than the one curl sent to.</summary>
    /// <param name="datagram">The datagram to mark.</param>
    /// <returns>The same datagram.</returns>
    public static byte[] FromAnotherEndpoint(byte[] datagram)
    {
        FromAnotherEndpointMarks.AddOrUpdate(datagram, datagram);
        return datagram;
    }

    /// <summary>Says whether <see cref="FromAnotherEndpoint" /> marked the datagram.</summary>
    /// <param name="datagram">The datagram.</param>
    /// <returns><see langword="true" /> when it was marked.</returns>
    internal static bool IsFromAnotherEndpoint(byte[] datagram) => FromAnotherEndpointMarks.TryGetValue(datagram, out _);

    /// <summary>A piggybacked response (section 5.2.1): an Acknowledgement echoing the request's Message ID and token.</summary>
    /// <param name="request">The request it answers.</param>
    /// <param name="code">The response code.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="options">The response's options.</param>
    /// <returns>The datagram.</returns>
    public static byte[] Piggybacked(CoapReferenceMessage request, byte code, byte[] payload, params IReadOnlyList<CoapReferenceOption> options)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CoapReferenceMessage(1, CoapReferenceMessageType.Acknowledgement, code, request.MessageId, request.Token, options, payload).Encode();
    }

    /// <summary>A separate response (section 5.2.2): a Confirmable or Non-confirmable message of its own Message ID carrying the request's token.</summary>
    /// <param name="request">The request it answers.</param>
    /// <param name="type">Confirmable or Non-confirmable.</param>
    /// <param name="messageId">The response's own Message ID.</param>
    /// <param name="code">The response code.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The datagram.</returns>
    public static byte[] Separate(CoapReferenceMessage request, CoapReferenceMessageType type, ushort messageId, byte code, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CoapReferenceMessage(1, type, code, messageId, request.Token, [], payload).Encode();
    }

    /// <summary>An Empty Acknowledgement of the request (section 4.2), the first half of a separate response.</summary>
    /// <param name="request">The request it acknowledges.</param>
    /// <returns>The datagram.</returns>
    public static byte[] EmptyAcknowledgement(CoapReferenceMessage request) => Empty(CoapReferenceMessageType.Acknowledgement, request);

    /// <summary>A Reset of the request (section 4.2): Empty, echoing its Message ID.</summary>
    /// <param name="request">The request it rejects.</param>
    /// <returns>The datagram.</returns>
    public static byte[] Reset(CoapReferenceMessage request) => Empty(CoapReferenceMessageType.Reset, request);

    private static byte[] Empty(CoapReferenceMessageType type, CoapReferenceMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CoapReferenceMessage(1, type, 0, request.MessageId, [], [], []).Encode();
    }
}
