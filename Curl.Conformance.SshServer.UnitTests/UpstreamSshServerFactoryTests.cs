using System.Text;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class UpstreamSshServerFactoryTests
{
    [TestMethod]
    public void Create_WindowsBuildOfCurl_GivesTheRsaClientKeysAndRsaHostKeyFingerprints()
    {
        UpstreamSshServer server = UpstreamSshServerFactory.Create(windowsBuildOfCurl: true);

        Assert.AreEqual(SshServerClientAccount.User, server.User);
        Assert.AreEqual(SshServerRsaClientKey.PrivateKeyPem, Encoding.ASCII.GetString(server.ClientPrivateKeyFile.Span));
        Assert.AreEqual(SshServerRsaClientKey.PublicKeyLine + "\n", Encoding.ASCII.GetString(server.ClientPublicKeyFile.Span));
        Assert.AreEqual(SshServerRsaHostKey.Md5Fingerprint, server.HostKeyMd5);
        Assert.AreEqual(SshServerRsaHostKey.Sha256Fingerprint, server.HostKeySha256);
    }

    [TestMethod]
    public void Create_OtherBuildOfCurl_GivesTheEd25519ClientKeysAndEd25519HostKeyFingerprints()
    {
        UpstreamSshServer server = UpstreamSshServerFactory.Create(windowsBuildOfCurl: false);

        Assert.AreEqual(SshServerClientAccount.User, server.User);
        CollectionAssert.AreEqual(SshServerClientAccount.CreatePrivateKeyFile(), server.ClientPrivateKeyFile.ToArray());
        CollectionAssert.AreEqual(SshServerClientAccount.CreatePublicKeyFile(), server.ClientPublicKeyFile.ToArray());
        Assert.AreEqual(SshServerHostKey.Md5Fingerprint, server.HostKeyMd5);
        Assert.AreEqual(SshServerHostKey.Sha256Fingerprint, server.HostKeySha256);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Create_EitherBuild_CreateServerMakesAFreshSshServerConnectorEachCall(bool windowsBuildOfCurl)
    {
        UpstreamSshServer server = UpstreamSshServerFactory.Create(windowsBuildOfCurl);

        var first = server.CreateServer();
        var second = server.CreateServer();

        Assert.IsInstanceOfType<SshServerConnector>(first);
        Assert.IsInstanceOfType<SshServerConnector>(second);
        Assert.AreNotSame(first, second);
    }
}
