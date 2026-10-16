using Curl.Api.EngineContracts;

namespace Curl.Api.UnitTests.EngineContracts;

[TestClass]
public sealed class CurlVersionInfoTests
{
    private static readonly CurlVersionInfo Defaults = new();

    [TestMethod]
    public void New_Version_IsTheTargetedLibcurl()
    {
        Assert.AreEqual("8.22.0", Defaults.Version);
        Assert.AreEqual(0x081600, Defaults.VersionNumber);
    }

    [TestMethod]
    public void New_NothingBuiltIn_IsReported()
    {
        Assert.AreEqual("", Defaults.Host);
        Assert.IsEmpty(Defaults.Features);
        Assert.IsEmpty(Defaults.Protocols);
        Assert.IsNull(Defaults.TlsBackend);
    }

    [TestMethod]
    public void New_TwoInstances_AreEqual()
    {
        Assert.AreEqual(new CurlVersionInfo(), Defaults);
    }

    [TestMethod]
    public void Init_EveryProperty_KeepsItsValue()
    {
        var info = new CurlVersionInfo
        {
            Version = "8.22.1",
            VersionNumber = 0x081601,
            Host = "x86_64-pc-win32",
            Features = ["SSL", "IPv6"],
            Protocols = ["http", "https"],
            TlsBackend = "Schannel",
        };

        Assert.AreEqual("8.22.1", info.Version);
        Assert.AreEqual(0x081601, info.VersionNumber);
        Assert.AreEqual("x86_64-pc-win32", info.Host);
        CollectionAssert.AreEqual(new[] { "SSL", "IPv6" }, info.Features.ToArray());
        CollectionAssert.AreEqual(new[] { "http", "https" }, info.Protocols.ToArray());
        Assert.AreEqual("Schannel", info.TlsBackend);
    }
}
