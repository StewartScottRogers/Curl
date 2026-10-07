using Curl.Protocol.Ldap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins <see cref="LdapSaslSecurityLayer" /> apart from the handler: what it passes through from
/// the connection beneath, how it reads a buffer into a small destination, and what it disposes.
/// </summary>
[TestClass]
public sealed class LdapSaslSecurityLayerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void IsSecureAndRemoteEndPoint_AreTheConnectionsBeneath()
    {
        var connection = new ScriptedConnection();
        Diagnostics.Arrange("connection", "ScriptedConnection with nothing scripted");

        var layer = new LdapSaslSecurityLayer(connection, Authenticated());

        Diagnostics.Act("IsSecure", layer.IsSecure);
        Diagnostics.Act("RemoteEndPoint", layer.RemoteEndPoint);
        Diagnostics.Assert("IsSecure", false, layer.IsSecure);
        Diagnostics.Assert("RemoteEndPoint", null, layer.RemoteEndPoint);
        Assert.IsFalse(layer.IsSecure);
        Assert.IsNull(layer.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReadAsync_DestinationSmallerThanTheBuffer_ReturnsTheRestOnTheNextRead()
    {
        byte[] incoming = Hex.Bytes($"00 00 00 13 {FakeLogonTokenSource.Signature(0)} 0a 0b 0c");
        var layer = new LdapSaslSecurityLayer(new ScriptedConnection(incoming), Authenticated());
        byte[] destination = new byte[2];
        Diagnostics.Bytes("incoming sealed buffer", incoming);
        Diagnostics.Arrange("destination size", destination.Length);

        int first = await layer.ReadAsync(destination, CancellationToken.None);
        int second = await layer.ReadAsync(destination.AsMemory(0, 2), CancellationToken.None);

        Diagnostics.Act("first read", first);
        Diagnostics.Act("second read", second);
        Diagnostics.Assert("first read", 2, first);
        Diagnostics.Assert("second read", 1, second);
        Diagnostics.Assert("destination[0]", 0x0c, destination[0]);
        Assert.AreEqual(2, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual(0x0c, destination[0]);
    }

    [TestMethod]
    public async Task WriteAsync_Message_SendsItSealedAfterItsLength()
    {
        var connection = new ScriptedConnection();
        var layer = new LdapSaslSecurityLayer(connection, Authenticated());

        Diagnostics.Arrange("message", "0a 0b");

        await layer.WriteAsync(Hex.Bytes("0a 0b"), CancellationToken.None);
        await layer.FlushAsync(CancellationToken.None);

        byte[] expectedSent = Hex.Bytes($"00 00 00 12 {FakeLogonTokenSource.Signature(0)} 0a 0b");
        Diagnostics.Act("sent", connection.Sent.Length);
        Diagnostics.Bytes("sent", connection.Sent);
        Diagnostics.Diff("sent", expectedSent, connection.Sent);
        Diagnostics.Assert("sent length", expectedSent.Length, connection.Sent.Length);
        CollectionAssert.AreEqual(Hex.Bytes($"00 00 00 12 {FakeLogonTokenSource.Signature(0)} 0a 0b"), connection.Sent);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheAuthenticationAndLeavesTheConnectionOpen()
    {
        var tokens = new FakeLogonTokenSource(0);
        var connection = new ScriptedConnection();
        var layer = new LdapSaslSecurityLayer(connection, tokens.Start(LdapLogonPackage.Ntlm, "ldap/x"));

        Diagnostics.Arrange("package", LdapLogonPackage.Ntlm);

        await layer.DisposeAsync();

        Diagnostics.Act("authentication disposed count", tokens.Disposed);
        Diagnostics.Act("connection disposed", connection.IsDisposed);
        Diagnostics.Assert("authentication disposed count", 1, tokens.Disposed);
        Diagnostics.Assert("connection disposed", false, connection.IsDisposed);
        Assert.AreEqual(1, tokens.Disposed);
        Assert.IsFalse(connection.IsDisposed);
    }

    private static ILdapLogonAuthentication Authenticated() => new FakeLogonTokenSource(0).Start(LdapLogonPackage.Ntlm, "ldap/x");
}
