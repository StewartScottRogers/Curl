using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslMessageText"/> to <c>ossl_trace</c> in curl 8.21.0's
/// <c>lib/vtls/openssl.c</c> for the cases the measured exchanges in BL-405's Notes do not reach.
/// </summary>
[TestClass]
public sealed class OpenSslMessageTextTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0x0304, TlsContentType.RecordHeader)]
    [DataRow(0x0304, TlsContentType.InnerContentType)]
    [DataRow(0, TlsContentType.Handshake)]
    public void Line_HeaderInnerTypeOrNoVersion_IsNone(int version, TlsContentType contentType)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("version", version);
        diagnostics.Arrange("content type", contentType);

        string? line = OpenSslMessageText.Line(Message(version, contentType, [1]));

        diagnostics.Act("line", line ?? "(null)");
        diagnostics.Assert("line", "(null)", line ?? "(null)");
        Assert.IsNull(line);
    }

    [TestMethod]
    [DataRow(0x0300, "SSLv3")]
    [DataRow(0x0301, "TLSv1.0")]
    [DataRow(0x0302, "TLSv1.1")]
    [DataRow(0x0305, "(305)")]
    public void Line_Version_NamedAsCurlNamesIt(int version, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("version", version);
        string expectedLine = $"{expected} (OUT), TLS handshake, Client hello (1):";

        string? line = OpenSslMessageText.Line(Message(version, TlsContentType.Handshake, [1]));

        diagnostics.Act("line", line);
        diagnostics.Diff("line", expectedLine, line ?? string.Empty);
        Assert.AreEqual(expectedLine, line);
    }

    [TestMethod]
    public void Line_VersionOutsideSsl3Family_HasNoRecordTypeAndNoMessageName()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("version", 0x0002);

        string? line = OpenSslMessageText.Line(Message(0x0002, TlsContentType.Handshake, [1]));

        WriteLine(diagnostics, "(2) (OUT), , Unknown (1):", line);
        Assert.AreEqual("(2) (OUT), , Unknown (1):", line);
    }

    [TestMethod]
    public void Line_ContentTypeZero_HasNoRecordType()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("content type", 0);

        string? line = OpenSslMessageText.Line(Message(0x0304, 0, [1]));

        WriteLine(diagnostics, "TLSv1.3 (OUT), , Client hello (1):", line);
        Assert.AreEqual("TLSv1.3 (OUT), , Client hello (1):", line);
    }

    [TestMethod]
    public void Line_UnnamedContentType_IsTlsUnknown()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("content type", 99);

        string? line = OpenSslMessageText.Line(Message(0x0304, (TlsContentType)99, [2]));

        WriteLine(diagnostics, "TLSv1.3 (OUT), TLS Unknown, Server hello (2):", line);
        Assert.AreEqual("TLSv1.3 (OUT), TLS Unknown, Server hello (2):", line);
    }

    [TestMethod]
    [DataRow(TlsContentType.ApplicationData, new byte[] { 3 }, "TLS app data, Unknown (3)")]
    [DataRow(TlsContentType.Handshake, new byte[] { 24 }, "TLS handshake, Key update (24)")]
    [DataRow(TlsContentType.Handshake, new byte[0], "TLS handshake, Truncated message (0)")]
    [DataRow(TlsContentType.ChangeCipherSpec, new byte[0], "TLS change cipher, Truncated message (0)")]
    [DataRow(TlsContentType.Alert, new byte[] { 2 }, "TLS alert, Truncated message (0)")]
    [DataRow(TlsContentType.Alert, new byte[] { 2, 40 }, "TLS alert, handshake failure (552)")]
    [DataRow(TlsContentType.Alert, new byte[] { 2, 255 }, "TLS alert, unknown (767)")]
    public void Line_Message_NamedAsCurlAndOpenSslNameIt(TlsContentType contentType, byte[] bytes, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("content type", contentType);
        diagnostics.Bytes("message bytes", bytes);
        string expectedLine = $"TLSv1.2 (IN), {expected}:";

        string? line = OpenSslMessageText.Line(Message(0x0303, contentType, bytes) with { Sent = false });

        WriteLine(diagnostics, expectedLine, line);
        Assert.AreEqual(expectedLine, line);
    }

    private static void WriteLine(TestDiagnostics diagnostics, string expected, string? line)
    {
        diagnostics.Act("line", line);
        diagnostics.Diff("line", expected, line ?? string.Empty);
    }

    private static TlsMessageEvent Message(int version, TlsContentType contentType, byte[] bytes)
    {
        return new TlsMessageEvent { ProtocolVersion = version, ContentType = contentType, Sent = true, Bytes = bytes };
    }
}
