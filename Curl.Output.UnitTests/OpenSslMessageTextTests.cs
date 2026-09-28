using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslMessageText"/> to <c>ossl_trace</c> in curl 8.21.0's
/// <c>lib/vtls/openssl.c</c> for the cases the measured exchanges in BL-405's Notes do not reach.
/// </summary>
[TestClass]
public sealed class OpenSslMessageTextTests
{
    [TestMethod]
    [DataRow(0x0304, TlsContentType.RecordHeader)]
    [DataRow(0x0304, TlsContentType.InnerContentType)]
    [DataRow(0, TlsContentType.Handshake)]
    public void Line_HeaderInnerTypeOrNoVersion_IsNone(int version, TlsContentType contentType)
    {
        Assert.IsNull(OpenSslMessageText.Line(Message(version, contentType, [1])));
    }

    [TestMethod]
    [DataRow(0x0300, "SSLv3")]
    [DataRow(0x0301, "TLSv1.0")]
    [DataRow(0x0302, "TLSv1.1")]
    [DataRow(0x0305, "(305)")]
    public void Line_Version_NamedAsCurlNamesIt(int version, string expected)
    {
        Assert.AreEqual($"{expected} (OUT), TLS handshake, Client hello (1):", OpenSslMessageText.Line(Message(version, TlsContentType.Handshake, [1])));
    }

    [TestMethod]
    public void Line_VersionOutsideSsl3Family_HasNoRecordTypeAndNoMessageName()
    {
        Assert.AreEqual("(2) (OUT), , Unknown (1):", OpenSslMessageText.Line(Message(0x0002, TlsContentType.Handshake, [1])));
    }

    [TestMethod]
    public void Line_ContentTypeZero_HasNoRecordType()
    {
        Assert.AreEqual("TLSv1.3 (OUT), , Client hello (1):", OpenSslMessageText.Line(Message(0x0304, 0, [1])));
    }

    [TestMethod]
    public void Line_UnnamedContentType_IsTlsUnknown()
    {
        Assert.AreEqual("TLSv1.3 (OUT), TLS Unknown, Server hello (2):", OpenSslMessageText.Line(Message(0x0304, (TlsContentType)99, [2])));
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
        Assert.AreEqual($"TLSv1.2 (IN), {expected}:", OpenSslMessageText.Line(Message(0x0303, contentType, bytes) with { Sent = false }));
    }

    private static TlsMessageEvent Message(int version, TlsContentType contentType, byte[] bytes)
    {
        return new TlsMessageEvent { ProtocolVersion = version, ContentType = contentType, Sent = true, Bytes = bytes };
    }
}
