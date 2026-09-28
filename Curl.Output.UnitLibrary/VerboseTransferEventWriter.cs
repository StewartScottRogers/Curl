using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Writes the <c>-v</c> lines for each transfer event as curl 8.21.0 does: <c>* </c> info
/// lines, <c>&gt; </c> request header lines, <c>&lt; </c> response header lines, and
/// <c>{ [N bytes data]</c> and <c>} [N bytes data]</c> for body bytes (ADR-0046).
/// </summary>
/// <param name="output">
/// The stream the lines are written to, not owned. Lines end in a bare line feed, the bytes
/// curl hands to <c>fwrite</c>; on Windows the caller wraps standard error so each line feed
/// becomes CR LF, as curl's text-mode standard error does.
/// </param>
/// <param name="writesDataLines">
/// Whether body bytes are shown as <c>[N bytes data]</c> lines. curl shows them only when
/// standard output is not a terminal, or when the lines do not go to standard output or
/// standard error; the caller decides.
/// </param>
/// <param name="writesTimestamps">
/// Whether each line that starts an event carries curl's <c>--trace-time</c> stamp,
/// <c>HH:MM:SS.uuuuuu </c> in local time, in front of its prefix.
/// </param>
/// <param name="timeProvider">The clock read for each stamp, when an event arrives.</param>
/// <param name="tlsBackend">The curl build whose wording a TLS handshake gets (ADR-0085).</param>
/// <remarks>
/// A port of the <c>-v</c> branch of <c>tool_debug_cb</c> in curl's <c>src/tool_cb_dbg.c</c>.
/// A run of body events with nothing between them is one data line carrying the first
/// event's byte count, as curl prints it. The structured events are worded by
/// <see cref="TransferEventInfoText"/>. TLS record bytes print nothing, as in curl's Schannel build.
/// The lines of one request header event share one stamp, as curl reads its clock once per
/// callback; a line that continues an open one gets neither stamp nor prefix.
/// </remarks>
public sealed class VerboseTransferEventWriter(
    Stream output,
    bool writesDataLines,
    bool writesTimestamps,
    TimeProvider timeProvider,
    TlsBackend tlsBackend) : ITransferEvents
{
    private const byte LineFeed = (byte)'\n';

    private static readonly byte[] InfoPrefix = "* "u8.ToArray();
    private static readonly byte[] ResponseHeaderPrefix = "< "u8.ToArray();
    private static readonly byte[] RequestHeaderPrefix = "> "u8.ToArray();
    private static readonly byte[] DataReceivedPrefix = "{ "u8.ToArray();
    private static readonly byte[] DataSentPrefix = "} "u8.ToArray();

    // curl's newl: the last line written stopped short of its line feed.
    private bool lineIsOpen;

    // curl's traced_data: a data line has been written since the last non-data event.
    private bool dataLineWritten;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerboseTransferEventWriter"/> class that
    /// words a TLS handshake as the running platform's curl build does
    /// (<see cref="PlatformTlsBackend.ForProcess"/>).
    /// </summary>
    /// <param name="output">The stream the lines are written to, not owned.</param>
    /// <param name="writesDataLines">Whether body bytes are shown as <c>[N bytes data]</c> lines.</param>
    public VerboseTransferEventWriter(Stream output, bool writesDataLines)
        : this(output, writesDataLines, PlatformTlsBackend.ForProcess)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VerboseTransferEventWriter"/> class that
    /// writes no <c>--trace-time</c> stamps.
    /// </summary>
    /// <param name="output">The stream the lines are written to, not owned.</param>
    /// <param name="writesDataLines">Whether body bytes are shown as <c>[N bytes data]</c> lines.</param>
    /// <param name="tlsBackend">The curl build whose wording a TLS handshake gets (ADR-0085).</param>
    public VerboseTransferEventWriter(Stream output, bool writesDataLines, TlsBackend tlsBackend)
        : this(output, writesDataLines, writesTimestamps: false, TimeProvider.System, tlsBackend)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VerboseTransferEventWriter"/> class that
    /// words a TLS handshake as the running platform's curl build does
    /// (<see cref="PlatformTlsBackend.ForProcess"/>).
    /// </summary>
    /// <param name="output">The stream the lines are written to, not owned.</param>
    /// <param name="writesDataLines">Whether body bytes are shown as <c>[N bytes data]</c> lines.</param>
    /// <param name="writesTimestamps">Whether each line that starts an event carries curl's <c>--trace-time</c> stamp.</param>
    /// <param name="timeProvider">The clock read for each stamp, when an event arrives.</param>
    public VerboseTransferEventWriter(Stream output, bool writesDataLines, bool writesTimestamps, TimeProvider timeProvider)
        : this(output, writesDataLines, writesTimestamps, timeProvider, PlatformTlsBackend.ForProcess)
    {
    }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        WriteTextLine(text);
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        WriteTextLine(TransferEventInfoText.ConnectionOpened(opened));
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused)
    {
        WriteTextLine(TransferEventInfoText.ConnectionReused(reused));
    }

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
        foreach (string line in TransferEventInfoText.TlsHandshake(handshake, tlsBackend))
        {
            WriteTextLine(line);
        }
    }

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
    {
    }

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            FinishHeader(bytes);
            return;
        }

        byte[] stamp = Timestamp();
        int start = 0;
        for (int index = 0; index < bytes.Length - 1; index++)
        {
            if (bytes[index] == LineFeed)
            {
                WriteLineStartUnlessLineIsOpen(stamp, RequestHeaderPrefix);
                output.Write(bytes[start..(index + 1)]);
                start = index + 1;
                lineIsOpen = false;
            }
        }

        WriteLineStartUnlessLineIsOpen(stamp, RequestHeaderPrefix);
        output.Write(bytes[start..]);
        FinishHeader(bytes);
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
        WriteLineStartUnlessLineIsOpen(ResponseHeaderPrefix);
        output.Write(bytes);
        FinishHeader(bytes);
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
        WriteDataLine(DataSentPrefix, bytes.Length);
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
        WriteDataLine(DataReceivedPrefix, bytes.Length);
    }

    private void WriteTextLine(string text)
    {
        WriteLineStartUnlessLineIsOpen(InfoPrefix);
        output.Write(Encoding.UTF8.GetBytes(text + "\n"));
        lineIsOpen = false;
        dataLineWritten = false;
    }

    private void FinishHeader(ReadOnlySpan<byte> bytes)
    {
        lineIsOpen = !bytes.IsEmpty && bytes[^1] != LineFeed;
        dataLineWritten = false;
    }

    private void WriteDataLine(byte[] prefix, int byteCount)
    {
        if (dataLineWritten || !writesDataLines)
        {
            return;
        }

        WriteLineStartUnlessLineIsOpen(prefix);
        output.Write(Encoding.ASCII.GetBytes($"[{byteCount} bytes data]\n"));
        lineIsOpen = false;
        dataLineWritten = true;
    }

    private void WriteLineStartUnlessLineIsOpen(byte[] prefix)
    {
        if (!lineIsOpen)
        {
            output.Write(Timestamp());
            output.Write(prefix);
        }
    }

    private void WriteLineStartUnlessLineIsOpen(byte[] stamp, byte[] prefix)
    {
        if (!lineIsOpen)
        {
            output.Write(stamp);
            output.Write(prefix);
        }
    }

    // curl's timebuf and log_line_start: the stamp goes in front of the prefix.
    private byte[] Timestamp()
    {
        return writesTimestamps ? Encoding.ASCII.GetBytes(TraceTimeStamp.Read(timeProvider)) : [];
    }
}
