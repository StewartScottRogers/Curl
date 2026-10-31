using Curl.Tls;

namespace Curl.Extension.Protocol.Coap.Dtls.Handshake;

[TestClass]
public sealed class DtlsClientHelloTests
{
    private static readonly byte[] Random = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];

    [TestMethod]
    public void Encode_Rfc6347Section4_2_1_PutsTheCookieAfterTheSessionIdThenTheSuitesAndNullCompression()
    {
        byte[] body = DtlsClientHello.Encode(Random, [0xC0, 0x0C], "192.0.2.1");

        CollectionAssert.AreEqual(new byte[] { 0xFE, 0xFD }, body[..2]);
        CollectionAssert.AreEqual(Random, body[2..34]);
        CollectionAssert.AreEqual(new byte[] { 0, 2, 0xC0, 0x0C, 0, 6, 0xC0, 0x2B, 0xC0, 0x2F, 0xC0, 0xAE, 1, 0 }, body[34..48]);
        Assert.AreEqual(body.Length - 50, (body[48] << 8) | body[49]);
    }

    [TestMethod]
    public void ExtensionsFor_Rfc6066Section3_HostName_AddsServerName()
    {
        List<TlsExtension> extensions = DtlsClientHello.ExtensionsFor("coap.example");

        CollectionAssert.AreEqual(
            new[] { TlsExtensionType.SupportedGroups, TlsExtensionType.EcPointFormats, TlsExtensionType.SignatureAlgorithms, TlsExtensionType.ExtendedMasterSecret, TlsExtensionType.ServerName },
            extensions.Select(extension => extension.Type).ToArray());
    }

    [TestMethod]
    public void ExtensionsFor_Rfc6066Section3_IpAddress_LeavesOutServerName()
    {
        List<TlsExtension> extensions = DtlsClientHello.ExtensionsFor("::1");

        Assert.IsFalse(extensions.Any(extension => extension.Type == TlsExtensionType.ServerName));
    }
}
