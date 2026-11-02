namespace Curl.Conformance.Extended.Coap.BlockwiseObserve;

/// <summary>The notifications an observing <see cref="CoapResponder" /> sends (RFC 7641 section 4.2).</summary>
public static class CoapNotifications
{
    /// <summary>A notification: a Confirmable or Non-confirmable message of its own Message ID carrying the registration's token and options.</summary>
    /// <param name="registration">The registration it answers.</param>
    /// <param name="type">Confirmable or Non-confirmable.</param>
    /// <param name="messageId">The notification's own Message ID.</param>
    /// <param name="code">The response code.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="options">The options; Observe among them for a notification, none to end the observation.</param>
    /// <returns>The datagram.</returns>
    public static byte[] Notification(CoapReferenceMessage registration, CoapReferenceMessageType type, ushort messageId, byte code, byte[] payload, params IReadOnlyList<CoapReferenceOption> options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return new CoapReferenceMessage(1, type, code, messageId, registration.Token, options, payload).Encode();
    }
}
