using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Writes the MQTT transfer steps to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Mqtt" /> (ADR-0222, BL-928): the failure that ends a
/// transfer as <c>error</c>, a packet the session does not expect and passes over as
/// <c>warning</c>, the SUBSCRIBE or PUBLISH sent, the CONNACK's return code, the SUBSCRIBE
/// or PUBLISH done and the transfer's end as <c>info</c>, and each packet's type and
/// remaining length, sent and received, as <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. A packet is logged by its type and remaining
/// length alone, never its body, so the CONNECT's user name and password are never written
/// (ADR-0222, decision 7), nor is a PUBLISH's payload.
/// </remarks>
internal sealed class MqttDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>The MQTT 3.1.1 packet type names (section 2.2.1), indexed by the type's nibble.</summary>
    private static readonly string[] PacketTypeNames =
    [
        "reserved type 0", "CONNECT", "CONNACK", "PUBLISH", "PUBACK", "PUBREC", "PUBREL", "PUBCOMP",
        "SUBSCRIBE", "SUBACK", "UNSUBSCRIBE", "UNSUBACK", "PINGREQ", "PINGRESP", "DISCONNECT", "reserved type 15",
    ];

    /// <summary>
    /// Logs, at <c>verbose</c>, a packet sent: its type and remaining length, never its body.
    /// </summary>
    /// <param name="packet">The whole packet, fixed header first.</param>
    public void PacketSent(byte[] packet)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(
                CultureInfo.InvariantCulture,
                $"sent {NameOf(packet[0])}, remaining length {RemainingLengthOf(packet)}"));
        }
    }

    /// <summary>Logs, at <c>verbose</c>, a fixed header received: the packet's type and remaining length.</summary>
    /// <param name="header">The header read.</param>
    public void PacketReceived(MqttFixedHeader header)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(
                CultureInfo.InvariantCulture,
                $"received {NameOf(header.FirstByte)}, remaining length {header.RemainingLength}"));
        }
    }

    /// <summary>Logs, at <c>info</c>, the CONNACK's return code.</summary>
    /// <param name="returnCode">The CONNACK's second byte; 0 is accepted.</param>
    public void ConnackReceived(byte returnCode)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(CultureInfo.InvariantCulture, $"CONNACK return code {returnCode}"));
        }
    }

    /// <summary>Logs, at <c>info</c>, the SUBSCRIBE about to be sent.</summary>
    /// <param name="topic">The decoded topic.</param>
    public void Subscribing(byte[] topic)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "SUBSCRIBE " + Encoding.UTF8.GetString(topic));
        }
    }

    /// <summary>Logs, at <c>info</c>, the PUBLISH about to be sent, with its payload's size.</summary>
    /// <param name="topic">The decoded topic.</param>
    /// <param name="payloadLength">The payload's length in bytes.</param>
    public void Publishing(byte[] topic, int payloadLength)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"PUBLISH {Encoding.UTF8.GetString(topic)}, {payloadLength} bytes"));
        }
    }

    /// <summary>Logs, at <c>info</c>, that the PUBLISH and the DISCONNECT after it are sent.</summary>
    public void PublishDone() => Info("PUBLISH done; DISCONNECT sent");

    /// <summary>Logs, at <c>info</c>, that a SUBACK granted the SUBSCRIBE.</summary>
    public void SubscribeDone() => Info("SUBSCRIBE done: SUBACK granted QoS 0");

    /// <summary>
    /// Logs, at <c>warning</c>, a packet with a body taken as the CONNACK although its type is
    /// not CONNACK, as curl checks only its length and bytes.
    /// </summary>
    /// <param name="header">The packet's header.</param>
    public void TakenAsConnack(MqttFixedHeader header) =>
        Unexpected(header, "taken as the CONNACK");

    /// <summary>
    /// Logs, at <c>warning</c>, an empty packet other than DISCONNECT or PINGRESP, which
    /// drops what the session awaited, so the next packet's body is left unread.
    /// </summary>
    /// <param name="header">The packet's header.</param>
    public void EmptyPacketIgnored(MqttFixedHeader header) =>
        Unexpected(header, "ignored; the next packet's body is left unread");

    /// <summary>Logs, at <c>warning</c>, a packet whose body is left unread.</summary>
    /// <param name="header">The packet's header.</param>
    public void BodyLeftUnread(MqttFixedHeader header) =>
        Unexpected(header, "body left unread");

    /// <summary>
    /// Logs, at <c>warning</c>, a packet that ends the transfer as curl's
    /// <c>State not handled yet</c> does, with exit 0.
    /// </summary>
    /// <param name="header">The packet's header.</param>
    public void StateNotHandled(MqttFixedHeader header) =>
        Unexpected(header, "not handled; the transfer ends");

    /// <summary>
    /// Logs how the transfer ended: <c>info</c> with the bytes and milliseconds for a success,
    /// <c>error</c> with the <see cref="CurlExitCode" /> and message for a failure.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="elapsed">How long the transfer took.</param>
    public void TransferEnded(TransferResult result, TimeSpan elapsed)
    {
        if (!result.IsSuccess)
        {
            Failed(result);
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"transfer finished: {result.BytesTransferred} bytes in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>
    /// The number of bytes after a packet's fixed header: its length less the type byte and
    /// the one to four remaining-length bytes, each but the last flagged <c>0x80</c>.
    /// </summary>
    private static int RemainingLengthOf(byte[] packet)
    {
        int lastLengthByte = 1;
        while ((packet[lastLengthByte] & 0x80) != 0)
        {
            lastLengthByte++;
        }

        return packet.Length - lastLengthByte - 1;
    }

    private static string NameOf(byte firstByte) => PacketTypeNames[firstByte >> 4];

    private void Failed(TransferResult result)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, string.Create(
                CultureInfo.InvariantCulture,
                $"failed with {result.ExitCode} ({(int)result.ExitCode}): {result.ErrorMessage}"));
        }
    }

    private void Unexpected(MqttFixedHeader header, string consequence)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(
                CultureInfo.InvariantCulture,
                $"unexpected {NameOf(header.FirstByte)}, remaining length {header.RemainingLength}: {consequence}"));
        }
    }

    private void Info(string message)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, message);
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Mqtt, message);
}
