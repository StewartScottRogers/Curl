using System.Security.Authentication;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="HandBuiltHandshake" /> names the negotiated version for the handshake
/// event, and what a failed handshake carries.
/// </summary>
[TestClass]
public sealed class HandBuiltHandshakeTests
{
    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls10, TlsVersionRange.Tls10)]
    [DataRow(TlsProtocolVersion.Tls11, TlsVersionRange.Tls11)]
    [DataRow(TlsProtocolVersion.Tls12, SslProtocols.Tls12)]
    public void ToSslProtocols_NamesTheVersionAsSslStreamDoes(TlsProtocolVersion version, SslProtocols expected) =>
        Assert.AreEqual(expected, HandBuiltHandshake.ToSslProtocols(version));

    [TestMethod]
    public void NegotiatedBy_NoChainPresented_NegotiatedNothing()
    {
        var handshake = HandBuiltHandshake.NegotiatedBy(null);

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

        var handshake = HandBuiltHandshake.NegotiatedBy(presented);

        Assert.AreEqual(expected, handshake.ProtocolVersion);
        Assert.AreEqual(0x1302, handshake.CipherSuite);
        Assert.AreEqual("h2", handshake.ApplicationProtocol);
        Assert.IsNull(handshake.Failure);
    }

    [TestMethod]
    public void Failed_CarriesTheFailureAndNoStream()
    {
        var failure = new TlsHandshakeFailure(TlsAlertDescription.HandshakeFailure, null);

        var handshake = HandBuiltHandshake.Failed(failure);

        Assert.AreSame(failure, handshake.Failure);
        Assert.IsNull(handshake.Stream);
    }
}
