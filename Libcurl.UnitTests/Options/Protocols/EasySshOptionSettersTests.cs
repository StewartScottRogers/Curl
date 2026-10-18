using Curl.Api.Options.Protocols;
using Libcurl.Options;
using static Libcurl.UnitTests.Options.Protocols.ProtocolOptionAssert;

namespace Libcurl.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasySshOptionSettersTests
{
    [TestMethod]
    public void SetOption_SshAuthTypes_KeepsTheLow32BitsUnchecked()
    {
        var both = SshAuthenticationTypes.PublicKey | SshAuthenticationTypes.Password;

        Assert.AreEqual(both, SetOk(CurlOption.SshAuthTypes, 3L).Ssh.AuthenticationTypes);
        Assert.AreEqual(SshAuthenticationTypes.Any, SetOk(CurlOption.SshAuthTypes, -1L).Ssh.AuthenticationTypes);
        Assert.AreEqual(SshAuthenticationTypes.Agent, SetOk(CurlOption.SshAuthTypes, SshAuthenticationTypes.Agent).Ssh.AuthenticationTypes);
        Assert.AreEqual(SshAuthenticationTypes.None, SetOk(CurlOption.SshAuthTypes, 0).Ssh.AuthenticationTypes);
    }

    [TestMethod]
    public void SetOption_SshCompression_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.SshCompression, p => p.Ssh.Compression);
    }

    [TestMethod]
    public void SetOption_SshHostPublicKeyMd5_TakesAnyStringAndNullClearsIt()
    {
        var md5 = new string('a', 32);
        var options = SetOk(new(), CurlOption.SshHostPublicKeyMd5, md5);

        Assert.AreEqual(md5, options.Protocols.Ssh.HostPublicKeyMd5);
        Assert.IsNull(SetOk(options, CurlOption.SshHostPublicKeyMd5, null).Protocols.Ssh.HostPublicKeyMd5);
        Assert.AreEqual("string", SetOk(options, CurlOption.SshHostPublicKeyMd5, "string").Protocols.Ssh.HostPublicKeyMd5);
    }

    [TestMethod]
    public void SetOption_SshHostPublicKeySha256_SetsTheFingerprintAndNullClearsIt()
    {
        Text(CurlOption.SshHostPublicKeySha256, p => p.Ssh.HostPublicKeySha256);
    }

    [TestMethod]
    public void SetOption_SshPublicKeyfile_SetsTheFileAndNullClearsIt()
    {
        Text(CurlOption.SshPublicKeyfile, p => p.Ssh.PublicKeyFile);
    }

    [TestMethod]
    public void SetOption_SshPrivateKeyfile_SetsTheFileAndNullClearsIt()
    {
        Text(CurlOption.SshPrivateKeyfile, p => p.Ssh.PrivateKeyFile);
    }

    [TestMethod]
    public void SetOption_SshKnownhosts_SetsTheFileAndNullClearsIt()
    {
        Text(CurlOption.SshKnownhosts, p => p.Ssh.KnownHostsFile);
    }
}
