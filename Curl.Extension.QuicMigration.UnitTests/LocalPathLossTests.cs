using System.Net.Sockets;

namespace Curl.Extension.QuicMigration;

/// <summary>Checks which failures <see cref="LocalPathLoss" /> counts as the local address change RFC 9000 section 9.2 lets a client migrate on.</summary>
[TestClass]
public sealed class LocalPathLossTests
{
    [TestMethod]
    [DataRow(SocketError.AddressNotAvailable)]
    [DataRow(SocketError.NetworkDown)]
    [DataRow(SocketError.NetworkUnreachable)]
    [DataRow(SocketError.NetworkReset)]
    [DataRow(SocketError.HostUnreachable)]
    public void Means_ALocalNetworkFailure_IsTrue(SocketError error) =>
        Assert.IsTrue(LocalPathLoss.Means(new SocketException((int)error)));

    [TestMethod]
    public void Means_AFailureThePeerCausedOrANonSocketFailure_IsFalse()
    {
        Assert.IsFalse(LocalPathLoss.Means(new SocketException((int)SocketError.ConnectionRefused)));
        Assert.IsFalse(LocalPathLoss.Means(new InvalidOperationException()));
    }
}
