namespace Curl.Protocol.Tftp;

/// <summary>
/// curl 8.21.0's words and rules for a packet whose opcode the transfer's state does not
/// handle (measured by BL-1435 and BL-1444): <c>tftp_receive_packet</c> notes
/// <c>Internal error: Unexpected packet</c> for any opcode but DATA, ACK, ERROR and OACK,
/// and the state machine then fails before the server has answered
/// (<c>tftp_send_first: internal error</c>, exit 71), fails a download that has
/// (<c>tftp_rx: internal error</c>, exit 71), and only notes it for an upload that has
/// (<c>tftp_tx: internal error, event: N</c>), which carries on.
/// </summary>
/// <remarks>
/// curl reads an opcode as its state machine's event, so opcode 0 (its INIT event) and
/// opcode 7 (its TIMEOUT event) before the server has answered re-send the request, and
/// opcode 7 after re-sends the last packet as a timeout would. An ACK as a download's first
/// reply turns it into a transmit and a DATA packet as an upload's first reply into a
/// receive (<see cref="TftpHandOver" />).
/// </remarks>
internal static class TftpUnexpectedOpcode
{
    /// <summary>The message <c>tftp_receive_packet</c> notes for an opcode it does not know.</summary>
    internal const string UnexpectedPacketMessage = "Internal error: Unexpected packet";

    /// <summary>The message curl notes when the server's first reply is an opcode it cannot start on.</summary>
    internal const string SendFirstMessage = "tftp_send_first: internal error";

    /// <summary>The message curl notes when a download that has started receives an opcode it does not handle.</summary>
    internal const string ReceiveMessage = "tftp_rx: internal error";

    /// <summary>The opcode curl reads as its TIMEOUT event.</summary>
    internal const ushort TimeoutEventOpcode = 7;

    /// <summary>The opcode curl reads as its INIT event.</summary>
    private const ushort InitEventOpcode = 0;

    /// <summary>
    /// Tells whether <c>tftp_receive_packet</c> notes <see cref="UnexpectedPacketMessage" />
    /// for the opcode: any but DATA, ACK, ERROR and OACK.
    /// </summary>
    /// <param name="opcode">The packet's opcode.</param>
    /// <returns><see langword="true" /> when curl notes the opcode as unexpected.</returns>
    internal static bool IsUnknown(ushort opcode) =>
        opcode is not (TftpPackets.DataOpcode
            or TftpPackets.AcknowledgementOpcode
            or TftpPackets.ErrorOpcode
            or TftpPackets.OptionAcknowledgementOpcode);

    /// <summary>
    /// Tells whether the opcode, as the server's first reply, re-sends the request: curl's
    /// INIT and TIMEOUT events, opcodes 0 and 7.
    /// </summary>
    /// <param name="opcode">The packet's opcode.</param>
    /// <returns><see langword="true" /> when curl re-sends the request.</returns>
    internal static bool ResendsTheRequest(ushort opcode) =>
        opcode is InitEventOpcode or TimeoutEventOpcode;
}
