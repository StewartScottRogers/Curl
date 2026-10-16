using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasySshOptionsTests
{
    private static readonly EasySshOptions Defaults = new();

    [TestMethod]
    public void New_EveryOption_DefaultsToLibcurlDefault()
    {
        Assert.AreEqual(SshAuthenticationTypes.Any, Defaults.AuthenticationTypes);
        Assert.IsFalse(Defaults.Compression);
        Assert.IsNull(Defaults.HostPublicKeyMd5);
        Assert.IsNull(Defaults.HostPublicKeySha256);
        Assert.IsNull(Defaults.PublicKeyFile);
        Assert.IsNull(Defaults.PrivateKeyFile);
        Assert.IsNull(Defaults.KnownHostsFile);
    }

    [TestMethod]
    public void SshAuthenticationTypes_CarryLibcurlBits()
    {
        Assert.AreEqual(0u, (uint)SshAuthenticationTypes.None);
        Assert.AreEqual(1u, (uint)SshAuthenticationTypes.PublicKey);
        Assert.AreEqual(2u, (uint)SshAuthenticationTypes.Password);
        Assert.AreEqual(4u, (uint)SshAuthenticationTypes.Host);
        Assert.AreEqual(8u, (uint)SshAuthenticationTypes.Keyboard);
        Assert.AreEqual(16u, (uint)SshAuthenticationTypes.Agent);
        Assert.AreEqual(32u, (uint)SshAuthenticationTypes.GssApi);
        Assert.AreEqual(0xFFFFFFFFu, (uint)SshAuthenticationTypes.Any);
    }

    public static IEnumerable<object[]> SingleOptionChanges =>
    [
        Change("AuthenticationTypes", o => o with { AuthenticationTypes = SshAuthenticationTypes.PublicKey | SshAuthenticationTypes.Agent }, o => o with { AuthenticationTypes = Defaults.AuthenticationTypes }),
        Change("Compression", o => o with { Compression = true }, o => o with { Compression = Defaults.Compression }),
        Change("HostPublicKeyMd5", o => o with { HostPublicKeyMd5 = "0123456789abcdef0123456789abcdef" }, o => o with { HostPublicKeyMd5 = Defaults.HostPublicKeyMd5 }),
        Change("HostPublicKeySha256", o => o with { HostPublicKeySha256 = "NDVkMTQxMGQ=" }, o => o with { HostPublicKeySha256 = Defaults.HostPublicKeySha256 }),
        Change("PublicKeyFile", o => o with { PublicKeyFile = "id.pub" }, o => o with { PublicKeyFile = Defaults.PublicKeyFile }),
        Change("PrivateKeyFile", o => o with { PrivateKeyFile = "id" }, o => o with { PrivateKeyFile = Defaults.PrivateKeyFile }),
        Change("KnownHostsFile", o => o with { KnownHostsFile = "known_hosts" }, o => o with { KnownHostsFile = Defaults.KnownHostsFile }),
    ];

    [TestMethod]
    [DynamicData(nameof(SingleOptionChanges))]
    public void With_OneOption_ChangesOnlyThatOption(
        string option,
        Func<EasySshOptions, EasySshOptions> set,
        Func<EasySshOptions, EasySshOptions> restore)
    {
        EasySshOptions changed = set(Defaults);

        Assert.AreNotEqual(Defaults, changed, $"setting {option} left the record unchanged");
        Assert.AreEqual(Defaults, restore(changed), $"setting {option} changed another option");
    }

    [TestMethod]
    public void SingleOptionChanges_CoverEveryProperty()
    {
        int properties = typeof(EasySshOptions).GetProperties().Length;

        Assert.HasCount(properties, SingleOptionChanges);
    }

    private static object[] Change(
        string option,
        Func<EasySshOptions, EasySshOptions> set,
        Func<EasySshOptions, EasySshOptions> restore) => [option, set, restore];
}
