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
    public void Failed_CarriesTheFailureAndNoStream()
    {
        var failure = new TlsHandshakeFailure(TlsAlertDescription.HandshakeFailure, null);

        var handshake = HandBuiltHandshake.Failed(failure);

        Assert.AreSame(failure, handshake.Failure);
        Assert.IsNull(handshake.Stream);
    }
}
