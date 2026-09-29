using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins <see cref="LdapSaslSecurityLayer" /> apart from the handler: what it passes through from
/// the connection beneath, how it reads a buffer into a small destination, and what it disposes.
/// </summary>
[TestClass]
public sealed class LdapSaslSecurityLayerTests
{
    [TestMethod]
    public void IsSecureAndRemoteEndPoint_AreTheConnectionsBeneath()
    {
        var connection = new ScriptedConnection();

        var layer = new LdapSaslSecurityLayer(connection, Authenticated());

        Assert.IsFalse(layer.IsSecure);
        Assert.IsNull(layer.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReadAsync_DestinationSmallerThanTheBuffer_ReturnsTheRestOnTheNextRead()
    {
        var layer = new LdapSaslSecurityLayer(new ScriptedConnection(Hex.Bytes($"00 00 00 13 {FakeLogonTokenSource.Signature(0)} 0a 0b 0c")), Authenticated());
        byte[] destination = new byte[2];

        int first = await layer.ReadAsync(destination, CancellationToken.None);
        int second = await layer.ReadAsync(destination.AsMemory(0, 2), CancellationToken.None);

        Assert.AreEqual(2, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual(0x0c, destination[0]);
    }

    [TestMethod]
    public async Task WriteAsync_Message_SendsItSealedAfterItsLength()
    {
        var connection = new ScriptedConnection();
        var layer = new LdapSaslSecurityLayer(connection, Authenticated());

        await layer.WriteAsync(Hex.Bytes("0a 0b"), CancellationToken.None);
        await layer.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual(Hex.Bytes($"00 00 00 12 {FakeLogonTokenSource.Signature(0)} 0a 0b"), connection.Sent);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheAuthenticationAndLeavesTheConnectionOpen()
    {
        var tokens = new FakeLogonTokenSource(0);
        var connection = new ScriptedConnection();
        var layer = new LdapSaslSecurityLayer(connection, tokens.Start(LdapLogonPackage.Ntlm, "ldap/x"));

        await layer.DisposeAsync();

        Assert.AreEqual(1, tokens.Disposed);
        Assert.IsFalse(connection.IsDisposed);
    }

    private static ILdapLogonAuthentication Authenticated() => new FakeLogonTokenSource(0).Start(LdapLogonPackage.Ntlm, "ldap/x");
}
