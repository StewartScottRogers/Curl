namespace Curl.Tls;

/// <summary>The outcome of <see cref="HandshakeMessageReader.Read" />.</summary>
/// <param name="Status">What was found.</param>
/// <param name="Message">The message read, when <paramref name="Status" /> is <see cref="HandshakeMessageReadStatus.Complete" />.</param>
/// <param name="BytesConsumed">The bytes the message took, header included; zero unless complete.</param>
/// <param name="Alert">The alert to send, when <paramref name="Status" /> is <see cref="HandshakeMessageReadStatus.Failed" />.</param>
public sealed record HandshakeMessageReadResult(
    HandshakeMessageReadStatus Status,
    HandshakeMessage? Message,
    int BytesConsumed,
    TlsAlertDescription? Alert)
{
    /// <summary>Gets the result for a buffer that does not yet hold a whole message.</summary>
    public static HandshakeMessageReadResult NeedMoreBytes { get; } = new(HandshakeMessageReadStatus.NeedMoreBytes, null, 0, null);

    /// <summary>Returns the result for a whole message.</summary>
    /// <param name="message">The message read.</param>
    /// <param name="bytesConsumed">The bytes it took, header included.</param>
    /// <returns>A complete result.</returns>
    public static HandshakeMessageReadResult Complete(HandshakeMessage message, int bytesConsumed) =>
        new(HandshakeMessageReadStatus.Complete, message, bytesConsumed, null);

    /// <summary>Returns the result for bytes that call for <paramref name="alert" />.</summary>
    /// <param name="alert">The alert to send.</param>
    /// <returns>A failed result.</returns>
    public static HandshakeMessageReadResult Failure(TlsAlertDescription alert) =>
        new(HandshakeMessageReadStatus.Failed, null, 0, alert);
}
