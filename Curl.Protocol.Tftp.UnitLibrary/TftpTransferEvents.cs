using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Reports a TFTP transfer's steps to <see cref="ITransferEvents" /> in curl 8.21.0's words,
/// for <c>-v</c>, <c>--trace</c> and <c>--trace-ascii</c> (ADR-0046): the connect lines,
/// each <c>set timeouts</c> line, the options an OACK carried, <c>Connected for receive</c>
/// or <c>transmit</c>, a retransmission's <c>Timeout waiting</c> line, an ERROR packet's
/// text, each downloaded block's bytes as data received, and <c>shutting down
/// connection</c> (measured by BL-933).
/// </summary>
/// <remarks>
/// An upload's DATA blocks are not reported as data sent: curl 8.21.0 reports none.
/// </remarks>
/// <param name="events">Where the steps are reported.</param>
internal sealed class TftpTransferEvents(ITransferEvents events)
{
    /// <summary>curl's number for the transfer's state before the server has answered.</summary>
    internal const int StartState = 0;

    /// <summary>curl's number for a download's state once the server has answered.</summary>
    internal const int ReceiveState = 1;

    /// <summary>curl's number for an upload's state once the server has answered.</summary>
    internal const int TransmitState = 2;

    /// <summary>
    /// Reports <c>  Trying &lt;endpoint&gt;...</c> and the <c>Established connection</c>
    /// line, whose local end curl leaves empty for an unconnected UDP socket.
    /// </summary>
    /// <param name="hostName">The host the URL names.</param>
    /// <param name="server">The server's endpoint the request goes to.</param>
    public void Connected(string hostName, EndPoint server)
    {
        events.ReportInfo($"  Trying {server}...");
        string remote = server is IPEndPoint address
            ? string.Create(CultureInfo.InvariantCulture, $"{address.Address} port {address.Port}")
            : server.ToString()!;
        events.ReportInfo($"Established connection to {hostName} ({remote}) from  port 0 ");
    }

    /// <summary>
    /// Reports <c>set timeouts for state N; Total T, retry R maxtry M</c>.
    /// </summary>
    /// <param name="state">curl's state number: <see cref="StartState" />, <see cref="ReceiveState" /> or <see cref="TransmitState" />.</param>
    /// <param name="schedule">The schedule just derived.</param>
    public void TimeoutsSet(int state, TftpRetrySchedule schedule) =>
        events.ReportInfo(string.Create(
            CultureInfo.InvariantCulture,
            $"set timeouts for state {state}; Total {schedule.TimeLeftMilliseconds}, retry {schedule.RetrySeconds} maxtry {schedule.RetryLimit}"));

    /// <summary>
    /// Reports each option an OACK carried as <c>got option=(name) value=(value)</c>,
    /// followed for <c>blksize</c> by the size parsed and the size requested, and for a
    /// download's <c>tsize</c> by the size parsed.
    /// </summary>
    /// <param name="options">The OACK's option bytes, after its opcode.</param>
    /// <param name="isDownload">Whether the transfer is a download.</param>
    /// <param name="requestedBlockSize">The <c>blksize</c> asked for.</param>
    public void OptionsAcknowledged(ReadOnlySpan<byte> options, bool isDownload, int requestedBlockSize)
    {
        string[] fields = Encoding.UTF8.GetString(options).TrimEnd('\0').Split('\0');
        for (int index = 0; index + 1 < fields.Length; index += 2)
        {
            string name = fields[index];
            string value = fields[index + 1];
            events.ReportInfo($"got option=({name}) value=({value})");
            ReportParsedBlockSize(name, value, requestedBlockSize);
            ReportParsedTransferSize(name, value, isDownload);
        }
    }

    /// <summary>Reports <c>Connected for receive</c> or <c>Connected for transmit</c>, then the answered state's timeouts.</summary>
    /// <param name="isDownload">Whether the transfer is a download.</param>
    /// <param name="schedule">The schedule derived once the server answered.</param>
    public void Answered(bool isDownload, TftpRetrySchedule schedule)
    {
        events.ReportInfo(isDownload ? "Connected for receive" : "Connected for transmit");
        TimeoutsSet(isDownload ? ReceiveState : TransmitState, schedule);
    }

    /// <summary>Reports <c>Timeout waiting for block N ACK. Retries = R</c>.</summary>
    /// <param name="block">The block curl waits for next.</param>
    /// <param name="retries">The retry count, this timeout included.</param>
    public void TimedOut(int block, int retries) =>
        events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"Timeout waiting for block {block} ACK. Retries = {retries}"));

    /// <summary>
    /// Reports <c>TFTP error: &lt;text&gt;</c> for an ERROR packet whose text ends in a NUL,
    /// and nothing for one whose text does not, as curl does.
    /// </summary>
    /// <param name="packet">The ERROR packet, at least four bytes long.</param>
    public void ErrorPacket(ReadOnlySpan<byte> packet)
    {
        ReadOnlySpan<byte> text = packet[TftpPackets.DataHeaderLength..];
        int end = text.IndexOf((byte)0);
        if (end >= 0)
        {
            events.ReportInfo($"TFTP error: {Encoding.UTF8.GetString(text[..end])}");
        }
    }

    /// <summary>Reports a downloaded block's bytes as data received, unless it is empty.</summary>
    /// <param name="payload">The block's bytes.</param>
    public void DataReceived(ReadOnlySpan<byte> payload)
    {
        if (!payload.IsEmpty)
        {
            events.ReportDataReceived(payload);
        }
    }

    /// <summary>Reports <c>shutting down connection #N</c>, N the number the channel was opened with (BL-969).</summary>
    /// <param name="connectionNumber">curl's number for the transfer's connection.</param>
    public void ShuttingDown(long connectionNumber) => events.ReportInfo($"shutting down connection #{connectionNumber}");

    private void ReportParsedBlockSize(string name, string value, int requestedBlockSize)
    {
        if (string.Equals(name, "blksize", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int blockSize)
            && blockSize is >= TftpPackets.MinimumBlockSize and <= TftpPackets.MaximumBlockSize)
        {
            events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"blksize parsed from OACK ({blockSize}) requested ({requestedBlockSize})"));
        }
    }

    private void ReportParsedTransferSize(string name, string value, bool isDownload)
    {
        if (isDownload
            && string.Equals(name, "tsize", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long transferSize))
        {
            events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"tsize parsed from OACK ({transferSize})"));
        }
    }
}
