using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="TraceTransferEventWriter"/>'s TLS message lines, SSL data dumps and
/// <c>SSL Trust</c> lines to what curl 8.21.0 (x86_64-pc-linux-musl, OpenSSL/3.5.7,
/// <c>curlimages/curl:8.21.0</c>) wrote for <c>--trace-ascii</c> on 2026-09-28 against Git
/// for Windows' <c>openssl s_server -www</c>; the command is in BL-450's Notes. Record
/// headers, change cipher specs, inner content types and Encrypted Extensions carry the
/// measured bytes; the other handshake messages carry their measured length and type byte,
/// zero-filled, as their measured bytes are random.
/// </summary>
[TestClass]
public sealed class TraceTransferEventWriterOpenSslTlsTests
{
    private const int Tls13 = 0x0304;
    private const byte HandshakeInnerType = 0x16;

    private readonly MemoryStream output = new();

    // `curl --trace-ascii - -k -s -o /dev/null https://host.docker.internal:28450/`, up to the handshake.
    [TestMethod]
    public void Tls13HandshakeWithoutVerification_TraceAscii_RendersAsCurlsOpenSslBuild()
    {
        var writer = OpenSslWriter(TraceDumpFormat.TextOnly);

        writer.ReportInfo("ALPN: curl offers h2,http/1.1");
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: true, 0x16, 0x03, 0x01, 0x06, 0x1E));
        writer.ReportTlsMessage(ZeroFilled(TlsContentType.Handshake, sent: true, 1566, 1));
        writer.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = false });
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: false, 0x16, 0x03, 0x03, 0x04, 0xBA));
        writer.ReportTlsMessage(ZeroFilled(TlsContentType.Handshake, sent: false, 1210, 2));
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: false, 0x14, 0x03, 0x03, 0x00, 0x01));
        writer.ReportTlsMessage(Message(TlsContentType.ChangeCipherSpec, sent: false, 0x01));
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: false, 0x17, 0x03, 0x03, 0x00, 0x17));
        writer.ReportTlsMessage(Message(TlsContentType.InnerContentType, sent: false, HandshakeInnerType));
        writer.ReportTlsMessage(Message(TlsContentType.Handshake, sent: false, 0x08, 0x00, 0x00, 0x02, 0x00, 0x00));
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: false, 0x17, 0x03, 0x03, 0x03, 0x47));
        writer.ReportTlsMessage(Message(TlsContentType.InnerContentType, sent: false, HandshakeInnerType));
        writer.ReportTlsMessage(ZeroFilled(TlsContentType.Handshake, sent: false, 822, 11));
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: false, 0x17, 0x03, 0x03, 0x01, 0x19));
        writer.ReportTlsMessage(Message(TlsContentType.InnerContentType, sent: false, HandshakeInnerType));
        writer.ReportTlsMessage(ZeroFilled(TlsContentType.Handshake, sent: false, 264, 15));
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: false, 0x17, 0x03, 0x03, 0x00, 0x45));
        writer.ReportTlsMessage(Message(TlsContentType.InnerContentType, sent: false, HandshakeInnerType));
        writer.ReportTlsMessage(ZeroFilled(TlsContentType.Handshake, sent: false, 52, 20));
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: true, 0x14, 0x03, 0x03, 0x00, 0x01));
        writer.ReportTlsMessage(Message(TlsContentType.ChangeCipherSpec, sent: true, 0x01));
        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: true, 0x17, 0x03, 0x03, 0x00, 0x45));
        writer.ReportTlsMessage(Message(TlsContentType.InnerContentType, sent: true, HandshakeInnerType));
        writer.ReportTlsMessage(ZeroFilled(TlsContentType.Handshake, sent: true, 52, 20));

        Assert.AreEqual(
            "* ALPN: curl offers h2,http/1.1\n" +
            "=> Send SSL data, 5 bytes (0x5)\n0000: .....\n" +
            "* TLSv1.3 (OUT), TLS handshake, Client hello (1):\n" +
            "=> Send SSL data, 1566 bytes (0x61e)\n" + UnprintableAsciiDump(1566) +
            "* SSL Trust: peer verification disabled\n" +
            "<= Recv SSL data, 5 bytes (0x5)\n0000: .....\n" +
            "* TLSv1.3 (IN), TLS handshake, Server hello (2):\n" +
            "<= Recv SSL data, 1210 bytes (0x4ba)\n" + UnprintableAsciiDump(1210) +
            "<= Recv SSL data, 5 bytes (0x5)\n0000: .....\n" +
            "* TLSv1.3 (IN), TLS change cipher, Change cipher spec (1):\n" +
            "<= Recv SSL data, 1 bytes (0x1)\n0000: .\n" +
            "<= Recv SSL data, 5 bytes (0x5)\n0000: .....\n" +
            "<= Recv SSL data, 1 bytes (0x1)\n0000: .\n" +
            "* TLSv1.3 (IN), TLS handshake, Encrypted Extensions (8):\n" +
            "<= Recv SSL data, 6 bytes (0x6)\n0000: ......\n" +
            "<= Recv SSL data, 5 bytes (0x5)\n0000: ....G\n" +
            "<= Recv SSL data, 1 bytes (0x1)\n0000: .\n" +
            "* TLSv1.3 (IN), TLS handshake, Certificate (11):\n" +
            "<= Recv SSL data, 822 bytes (0x336)\n" + UnprintableAsciiDump(822) +
            "<= Recv SSL data, 5 bytes (0x5)\n0000: .....\n" +
            "<= Recv SSL data, 1 bytes (0x1)\n0000: .\n" +
            "* TLSv1.3 (IN), TLS handshake, CERT verify (15):\n" +
            "<= Recv SSL data, 264 bytes (0x108)\n" + UnprintableAsciiDump(264) +
            "<= Recv SSL data, 5 bytes (0x5)\n0000: ....E\n" +
            "<= Recv SSL data, 1 bytes (0x1)\n0000: .\n" +
            "* TLSv1.3 (IN), TLS handshake, Finished (20):\n" +
            "<= Recv SSL data, 52 bytes (0x34)\n" + UnprintableAsciiDump(52) +
            "=> Send SSL data, 5 bytes (0x5)\n0000: .....\n" +
            "* TLSv1.3 (OUT), TLS change cipher, Change cipher spec (1):\n" +
            "=> Send SSL data, 1 bytes (0x1)\n0000: .\n" +
            "=> Send SSL data, 5 bytes (0x5)\n0000: ....E\n" +
            "=> Send SSL data, 1 bytes (0x1)\n0000: .\n" +
            "* TLSv1.3 (OUT), TLS handshake, Finished (20):\n" +
            "=> Send SSL data, 52 bytes (0x34)\n" + UnprintableAsciiDump(52),
            Written());
    }

    // The same exchange after the response body: the server's close notify.
    [TestMethod]
    public void Tls13CloseNotify_TraceAscii_RendersAsCurlsOpenSslBuild()
    {
        var writer = OpenSslWriter(TraceDumpFormat.TextOnly);

        writer.ReportTlsMessage(Message(TlsContentType.RecordHeader, sent: false, 0x17, 0x03, 0x03, 0x00, 0x13));
        writer.ReportTlsMessage(Message(TlsContentType.InnerContentType, sent: false, 0x15));
        writer.ReportTlsMessage(Message(TlsContentType.Alert, sent: false, 0x01, 0x00));

        Assert.AreEqual(
            "<= Recv SSL data, 5 bytes (0x5)\n0000: .....\n" +
            "<= Recv SSL data, 1 bytes (0x1)\n0000: .\n" +
            "* TLSv1.3 (IN), TLS alert, close notify (256):\n" +
            "<= Recv SSL data, 2 bytes (0x2)\n0000: ..\n",
            Written());
    }

    // --trace dumps SSL data as hex and text, as it dumps body bytes.
    [TestMethod]
    public void TlsData_Trace_DumpsHexAndText()
    {
        OpenSslWriter(TraceDumpFormat.HexAndText).ReportTlsData([0x17, 0x03, 0x03, 0x00, 0x45], sent: false);

        Assert.AreEqual(
            "<= Recv SSL data, 5 bytes (0x5)\n0000: 17 03 03 00 45 " + new string(' ', 11 * 3) + "....E\n",
            Written());
    }

    [TestMethod]
    public void TlsTrustWithCaFile_WritesTheTrustAnchorLines()
    {
        OpenSslWriter(TraceDumpFormat.TextOnly).ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true, CaCertificateFile = "/w/cert.pem" });

        Assert.AreEqual("* SSL Trust Anchors:\n*   CAfile: /w/cert.pem\n", Written());
    }

    [TestMethod]
    public void TlsMessage_WithTraceTime_StampsTheLineAndTheDump()
    {
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.TextOnly, writesTimestamps: true, new QueuedTimeProvider(
            [new DateTimeOffset(2026, 9, 28, 3, 33, 48, TimeSpan.Zero), new DateTimeOffset(2026, 9, 28, 3, 33, 48, TimeSpan.Zero)]), TlsBackend.OpenSsl);

        writer.ReportTlsMessage(Message(TlsContentType.ChangeCipherSpec, sent: true, 0x01));

        var lines = Written().Split('\n');
        StringAssert.EndsWith(lines[0], " * TLSv1.3 (OUT), TLS change cipher, Change cipher spec (1):");
        StringAssert.EndsWith(lines[1], " => Send SSL data, 1 bytes (0x1)");
        Assert.AreEqual("0000: .", lines[2]);
    }

    private static TlsMessageEvent Message(TlsContentType contentType, bool sent, params byte[] bytes)
    {
        return new TlsMessageEvent { ProtocolVersion = Tls13, ContentType = contentType, Sent = sent, Bytes = bytes };
    }

    private static TlsMessageEvent ZeroFilled(TlsContentType contentType, bool sent, int length, byte messageType)
    {
        var bytes = new byte[length];
        bytes[0] = messageType;
        return Message(contentType, sent, bytes);
    }

    // --trace-ascii's dump of bytes that are all unprintable: 64 dots to a line.
    private static string UnprintableAsciiDump(int length)
    {
        StringBuilder dump = new();
        for (int offset = 0; offset < length; offset += 0x40)
        {
            dump.Append(CultureInfo.InvariantCulture, $"{offset:x4}: ").Append('.', Math.Min(0x40, length - offset)).Append('\n');
        }

        return dump.ToString();
    }

    private TraceTransferEventWriter OpenSslWriter(TraceDumpFormat format)
    {
        return new TraceTransferEventWriter(output, format, writesTimestamps: false, TimeProvider.System, TlsBackend.OpenSsl);
    }

    private string Written()
    {
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
