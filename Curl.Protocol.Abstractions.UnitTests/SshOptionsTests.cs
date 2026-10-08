using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="SshOptions" />: a new instance holds curl's "not given" values, and every
/// member set in the initializer reads back unchanged (ADR-0122).
/// </summary>
[TestClass]
public sealed class SshOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SshOptions_NothingSet_HoldsCurlsNotGivenValues()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "new SshOptions()");

        var options = new SshOptions();

        diagnostics.Act("private key path", options.PrivateKeyPath);
        diagnostics.Act("known hosts path", options.KnownHostsPath);
        diagnostics.Act("compression", options.Compression);
        diagnostics.Assert("private key path", null, options.PrivateKeyPath);
        diagnostics.Assert("compression", false, options.Compression);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("private key path", "id_ed25519");
        diagnostics.Arrange("known hosts path", "known_hosts");
        diagnostics.Arrange("host public key sha256", "47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU");

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

        diagnostics.Act("private key path", options.PrivateKeyPath);
        diagnostics.Act("public key path", options.PublicKeyPath);
        diagnostics.Act("known hosts path", options.KnownHostsPath);
        diagnostics.Act("host public key md5", options.HostPublicKeyMd5);
        diagnostics.Act("compression", options.Compression);
        diagnostics.Assert("private key path", "id_ed25519", options.PrivateKeyPath);
        diagnostics.Assert("compression", true, options.Compression);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var without = new SshOptions { KnownHostsPath = "known_hosts" };
        var with = without with { Compression = true };
        diagnostics.Arrange("without", without);
        diagnostics.Arrange("with", with);

        bool differ = !without.Equals(with);
        bool sameContentEqual = without.Equals(new SshOptions { KnownHostsPath = "known_hosts" });

        diagnostics.Act("differ", differ);
        diagnostics.Act("same content equal", sameContentEqual);
        diagnostics.Assert("differ", true, differ);
        diagnostics.Assert("same content equal", true, sameContentEqual);
        Assert.AreNotEqual(without, with);
        Assert.AreEqual(without, new SshOptions { KnownHostsPath = "known_hosts" });
    }
}
