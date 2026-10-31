namespace Curl.Extended.Protocol.Coap.Observe;

/// <summary>
/// Orders notifications by RFC 7641 section 3.4: a notification is newer than the last one taken when its
/// sequence number is ahead of the last one's by less than 2^23 in the 24-bit space, or when it arrives more
/// than 128 seconds after the last one.
/// </summary>
internal static class CoapNotificationOrder
{
    /// <summary>How long after the last notification any notification is taken as newer (128 seconds).</summary>
    public static readonly TimeSpan FreshnessWindow = TimeSpan.FromSeconds(128);

    private const uint HalfSequenceSpace = 1u << 23;

    /// <summary>Tells whether a notification is newer than the last one taken.</summary>
    /// <param name="lastSequence">The last notification's Observe value, V1.</param>
    /// <param name="lastArrival">When the last notification arrived, T1.</param>
    /// <param name="sequence">The new notification's Observe value, V2.</param>
    /// <param name="arrival">When the new notification arrived, T2.</param>
    /// <returns><see langword="true" /> when the new notification is to be taken.</returns>
    public static bool IsNewer(uint lastSequence, DateTimeOffset lastArrival, uint sequence, DateTimeOffset arrival) =>
        (lastSequence < sequence && sequence - lastSequence < HalfSequenceSpace)
            || (lastSequence > sequence && lastSequence - sequence > HalfSequenceSpace)
            || arrival > lastArrival + FreshnessWindow;
}
