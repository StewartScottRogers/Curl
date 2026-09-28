namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="SshOptions" />: a new instance holds curl's "not given" values, and every
/// member set in the initializer reads back unchanged (ADR-0122).
/// </summary>
[TestClass]
public sealed class SshOptionsTests
{
    [TestMethod]
    public void SshOptions_NothingSet_HoldsCurlsNotGivenValues()
    {
        var options = new SshOptions();

        Assert.IsNull(options.PrivateKeyPath);
        Assert.IsNull(options.PublicKeyPath);
        Assert.IsNull(options.PrivateKeyPassphrase);
        Assert.IsNull(options.KnownHostsPath);
        Assert.IsNull(options.HostPublicKeyMd5);
        Assert.IsNull(options.HostPublicKeySha256);
        Assert.IsFalse(options.Compression);
    }

    [TestMethod]
    public void SshOptions_EveryMemberSet_RoundTripsEveryValue()
    {
        var options = new SshOptions
        {
            PrivateKeyPath = "id_ed25519",
            PublicKeyPath = "id_ed25519.pub",
            PrivateKeyPassphrase = "secret",
            KnownHostsPath = "known_hosts",
            HostPublicKeyMd5 = "0123456789abcdef0123456789abcdef",
            HostPublicKeySha256 = "47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU",
            Compression = true,
        };

        Assert.AreEqual("id_ed25519", options.PrivateKeyPath);
        Assert.AreEqual("id_ed25519.pub", options.PublicKeyPath);
        Assert.AreEqual("secret", options.PrivateKeyPassphrase);
        Assert.AreEqual("known_hosts", options.KnownHostsPath);
        Assert.AreEqual("0123456789abcdef0123456789abcdef", options.HostPublicKeyMd5);
        Assert.AreEqual("47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU", options.HostPublicKeySha256);
        Assert.IsTrue(options.Compression);
    }

    [TestMethod]
    public void Equals_ForOptionsDifferingOnlyInCompression_ReturnsFalse()
    {
        var without = new SshOptions { KnownHostsPath = "known_hosts" };
        var with = without with { Compression = true };

        Assert.AreNotEqual(without, with);
        Assert.AreEqual(without, new SshOptions { KnownHostsPath = "known_hosts" });
    }
}
