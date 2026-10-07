using System.Security.Authentication;

using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="HandBuiltHandshake" /> names the negotiated version for the handshake
/// event, and what a failed handshake carries.
/// </summary>
[TestClass]
public sealed class HandBuiltHandshakeTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls10, TlsVersionRange.Tls10)]
    [DataRow(TlsProtocolVersion.Tls11, TlsVersionRange.Tls11)]
    [DataRow(TlsProtocolVersion.Tls12, SslProtocols.Tls12)]
    public void ToSslProtocols_NamesTheVersionAsSslStreamDoes(TlsProtocolVersion version, SslProtocols expected)
    {
        Diagnostics.Arrange("version", version);

        var actual = HandBuiltHandshake.ToSslProtocols(version);

        Diagnostics.Act("SslProtocols", actual);
        Diagnostics.Assert("SslProtocols", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void NegotiatedBy_NoChainPresented_NegotiatedNothing()
    {
        Diagnostics.Arrange("presented chain", "null");

        var handshake = HandBuiltHandshake.NegotiatedBy(null);

        Diagnostics.Act("protocol version", handshake.ProtocolVersion);
        Diagnostics.Act("cipher suite", handshake.CipherSuite);
        Diagnostics.Act("application protocol", handshake.ApplicationProtocol ?? "null");
        Diagnostics.Assert("protocol version", SslProtocols.None, handshake.ProtocolVersion);
        Diagnostics.Assert("cipher suite", 0, handshake.CipherSuite);
        Diagnostics.Assert("application protocol", "null", handshake.ApplicationProtocol ?? "null");
        Assert.AreEqual(SslProtocols.None, handshake.ProtocolVersion);
        Assert.AreEqual(0, handshake.CipherSuite);
        Assert.IsNull(handshake.ApplicationProtocol);
    }

    [TestMethod]
    [DataRow((ushort)0x0304, SslProtocols.Tls13)]
    [DataRow((ushort)0x0303, SslProtocols.Tls12)]
    public void NegotiatedBy_AChainPresented_CarriesItsVersionSuiteAndApplicationProtocol(ushort wireVersion, SslProtocols expected)
    {
        var presented = new ServerCertificateChain([], "localhost", null) { ProtocolVersion = wireVersion, CipherSuite = 0x1302, ApplicationProtocol = "h2" };
        Diagnostics.Arrange("wire version", $"0x{wireVersion:x4}");
        Diagnostics.Arrange("cipher suite", "0x1302");
        Diagnostics.Arrange("application protocol", "h2");

        var handshake = HandBuiltHandshake.NegotiatedBy(presented);

        Diagnostics.Act("protocol version", handshake.ProtocolVersion);
        Diagnostics.Act("cipher suite", handshake.CipherSuite);
        Diagnostics.Act("application protocol", handshake.ApplicationProtocol);
        Diagnostics.Act("failure", handshake.Failure?.ToString() ?? "null");
        Diagnostics.Assert("protocol version", expected, handshake.ProtocolVersion);
        Diagnostics.Assert("cipher suite", 0x1302, handshake.CipherSuite);
        Diagnostics.Assert("application protocol", "h2", handshake.ApplicationProtocol);
        Diagnostics.Assert("failure is null", true, handshake.Failure is null);
        Assert.AreEqual(expected, handshake.ProtocolVersion);
        Assert.AreEqual(0x1302, handshake.CipherSuite);
        Assert.AreEqual("h2", handshake.ApplicationProtocol);
        Assert.IsNull(handshake.Failure);
    }

    [TestMethod]
    public void Failed_CarriesTheFailureAndNoStream()
    {
        var failure = new TlsHandshakeFailure(TlsAlertDescription.HandshakeFailure, null);
        Diagnostics.Arrange("failure alert", TlsAlertDescription.HandshakeFailure);

        var handshake = HandBuiltHandshake.Failed(failure);

        Diagnostics.Act("failure is the same", ReferenceEquals(failure, handshake.Failure));
        Diagnostics.Act("stream is null", handshake.Stream is null);
        Diagnostics.Assert("failure is the same", true, ReferenceEquals(failure, handshake.Failure));
        Diagnostics.Assert("stream is null", true, handshake.Stream is null);
        Assert.AreSame(failure, handshake.Failure);
        Assert.IsNull(handshake.Stream);
    }
}
