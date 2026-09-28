using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Writes the <c>--trace</c> and <c>--trace-ascii</c> dump for each transfer event as curl
/// 8.21.0 does: <c>* </c> info lines, and for header and body bytes a
/// <c>=&gt; Send header, N bytes (0xN)</c> style line followed by the bytes, with an
/// optional <c>--trace-time</c> stamp in front of each (ADR-0046).
/// </summary>
/// <param name="output">
/// The stream the dump is written to, not owned. Lines end in a bare line feed, the bytes
/// curl hands to <c>fprintf</c>; on Windows the caller wraps the trace file so each line
/// feed becomes CR LF, as curl's text-mode trace file does.
/// </param>
/// <param name="format">Whether the bytes are shown as hex and text or as text only.</param>
/// <param name="writesTimestamps">
/// Whether each line that starts an event carries curl's <c>--trace-time</c> stamp,
/// <c>HH:MM:SS.uuuuuu </c> in local time.
/// </param>
/// <param name="timeProvider">The clock read for each stamp, when an event arrives.</param>
/// <param name="tlsBackend">The curl build whose wording a TLS handshake gets (ADR-0085).</param>
/// <remarks>
/// A port of the trace branch of <c>tool_debug_cb</c> and of <c>dump</c> in curl's
/// <c>src/tool_cb_dbg.c</c>. Every body event is its own dump, unlike <c>-v</c>. The
/// structured events are worded by <see cref="TransferEventInfoText"/>. TLS messages, their
/// bytes and the trust a connection is set up with print only as curl's OpenSSL build
/// prints them, every TLS message and record header dumped as SSL data; the Schannel build
/// prints none of them.
/// </remarks>
public sealed class TraceTransferEventWriter(
    Stream output,
    TraceDumpFormat format,
    bool writesTimestamps,
    TimeProvider timeProvider,
    TlsBackend tlsBackend) : ITransferEvents
{
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const char UnprintableByte = '.';

    private readonly int bytesPerLine = format == TraceDumpFormat.HexAndText ? 0x10 : 0x40;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceTransferEventWriter"/> class that
    /// words a TLS handshake as the running platform's curl build does
    /// (<see cref="PlatformTlsBackend.ForProcess"/>).
    /// </summary>
    /// <param name="output">The stream the dump is written to, not owned.</param>
    /// <param name="format">Whether the bytes are shown as hex and text or as text only.</param>
    /// <param name="writesTimestamps">Whether each line that starts an event carries curl's <c>--trace-time</c> stamp.</param>
    /// <param name="timeProvider">The clock read for each stamp, when an event arrives.</param>
    public TraceTransferEventWriter(Stream output, TraceDumpFormat format, bool writesTimestamps, TimeProvider timeProvider)
        : this(output, format, writesTimestamps, timeProvider, PlatformTlsBackend.ForProcess)
    {
    }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        WriteInfoLine(text);
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        WriteInfoLine(TransferEventInfoText.ConnectionOpened(opened));
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused)
    {
        WriteInfoLine(TransferEventInfoText.ConnectionReused(reused));
    }

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
        foreach (string line in TransferEventInfoText.TlsHandshake(handshake, tlsBackend))
        {
            WriteInfoLine(line);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The OpenSSL build dumps the bytes as <c>=&gt; Send SSL data</c> or
    /// <c>&lt;= Recv SSL data</c>, as it dumps body bytes; the Schannel build writes nothing.
    /// </remarks>
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
    {
        if (tlsBackend == TlsBackend.OpenSsl)
        {
            WriteDump(sent ? "=> Send SSL data" : "<= Recv SSL data", bytes);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The OpenSSL build writes the message's line (<see cref="OpenSslMessageText"/>), when it
    /// has one, then dumps its bytes as <see cref="ReportTlsData"/> does, record headers and
    /// TLS 1.3 inner content types included; the Schannel build writes nothing.
    /// </remarks>
    public void ReportTlsMessage(TlsMessageEvent message)
    {
        if (tlsBackend == TlsBackend.OpenSsl && OpenSslMessageText.Line(message) is { } line)
        {
            WriteInfoLine(line);
        }

        ReportTlsData(message.Bytes.Span, message.Sent);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The OpenSSL build writes its <c>SSL Trust</c> lines (<see cref="OpenSslTrustText"/>);
    /// the Schannel build writes nothing.
    /// </remarks>
    public void ReportTlsTrust(TlsTrustEvent trust)
    {
        if (tlsBackend == TlsBackend.OpenSsl)
        {
            foreach (string line in OpenSslTrustText.Lines(trust))
            {
                WriteInfoLine(line);
            }
        }
    }

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
        WriteDump("=> Send header", bytes);
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
        WriteDump("<= Recv header", bytes);
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
        WriteDump("=> Send data", bytes);
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
        WriteDump("<= Recv data", bytes);
    }

    private void WriteInfoLine(string text)
    {
        output.Write(Encoding.UTF8.GetBytes(Timestamp() + "* " + text + "\n"));
    }

    private void WriteDump(string title, ReadOnlySpan<byte> bytes)
    {
        StringBuilder dump = new();
        dump.Append(CultureInfo.InvariantCulture, $"{Timestamp()}{title}, {bytes.Length} bytes (0x{bytes.Length:x})\n");
        int offset = 0;
        while (offset < bytes.Length)
        {
            dump.Append(CultureInfo.InvariantCulture, $"{offset:x4}: ");
            if (format == TraceDumpFormat.HexAndText)
            {
                AppendHex(dump, bytes, offset);
            }

            offset = AppendText(dump, bytes, offset);
            dump.Append('\n');
        }

        output.Write(Encoding.ASCII.GetBytes(dump.ToString()));
    }

    private void AppendHex(StringBuilder dump, ReadOnlySpan<byte> bytes, int offset)
    {
        for (int index = offset; index < offset + bytesPerLine; index++)
        {
            dump.Append(index < bytes.Length ? bytes[index].ToString("x2", CultureInfo.InvariantCulture) + " " : "   ");
        }
    }

    // Returns where the next line starts. --trace-ascii ends a line at a CR LF and skips it.
    private int AppendText(StringBuilder dump, ReadOnlySpan<byte> bytes, int offset)
    {
        for (int index = offset; index < offset + bytesPerLine && index < bytes.Length; index++)
        {
            if (EndsLineAt(bytes, index))
            {
                return index + 2;
            }

            dump.Append(ShownAsText(bytes[index]));
            if (EndsLineAt(bytes, index + 1))
            {
                return index + 3;
            }
        }

        return offset + bytesPerLine;
    }

    private static char ShownAsText(byte value)
    {
        return value is >= 0x20 and < 0x80 ? (char)value : UnprintableByte;
    }

    private bool EndsLineAt(ReadOnlySpan<byte> bytes, int index)
    {
        return format == TraceDumpFormat.TextOnly
            && index + 1 < bytes.Length
            && bytes[index] == CarriageReturn
            && bytes[index + 1] == LineFeed;
    }

    private string Timestamp()
    {
        if (!writesTimestamps)
        {
            return string.Empty;
        }

        return TraceTimeStamp.Read(timeProvider);
    }
}
