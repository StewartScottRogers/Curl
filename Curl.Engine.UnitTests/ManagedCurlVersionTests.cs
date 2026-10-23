using Curl.Api.EngineContracts;
using Curl.Cli;

namespace Curl.Engine;

[TestClass]
public sealed class ManagedCurlVersionTests
{
    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void For_EachPlatform_DescribesTheBuildCurlVDescribes(bool isWindows, bool isMacOS)
    {
        CurlVersionInfo info = ManagedCurlVersion.For(isWindows, isMacOS);

        AssertMatchesCurlV(info, CurlVersionText.Lines(isWindows, isMacOS));
    }

    [TestMethod]
    public async Task VersionInfo_ProductionEngine_DescribesTheBuildCurlVDescribesOnTheRunningPlatform()
    {
        await using ManagedCurlEngine engine = new();

        AssertMatchesCurlV(engine.VersionInfo, CurlVersionText.Lines(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()));
    }

    [TestMethod]
    public void For_AnyPlatform_ListsNeitherSmbNorSmbs()
    {
        CurlVersionInfo info = ManagedCurlVersion.For(isWindows: true, isMacOS: false);

        CollectionAssert.DoesNotContain(info.Protocols.ToArray(), "smb");
        CollectionAssert.DoesNotContain(info.Protocols.ToArray(), "smbs");
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void For_EachPlatform_ListsHttp2RightAfterHsts(bool isWindows, bool isMacOS)
    {
        IReadOnlyList<string> features = ManagedCurlVersion.For(isWindows, isMacOS).Features;

        int hsts = features.ToList().IndexOf("HSTS");
        Assert.IsGreaterThanOrEqualTo(0, hsts);
        Assert.AreEqual("HTTP2", features[hsts + 1]);
    }

    [TestMethod]
    public void For_TwoCalls_GiveTwoInstances()
    {
        Assert.AreNotSame(ManagedCurlVersion.For(true, false), ManagedCurlVersion.For(true, false));
    }

    // -V's first line is "curl 8.22.0 (<host>) libcurl/8.22.0 <backend>"; its Protocols: line adds the
    // tool's own ipfs and ipns to libcurl's protocols, and its Features: line is libcurl's features.
    private static void AssertMatchesCurlV(CurlVersionInfo info, IReadOnlyList<string> lines)
    {
        Assert.AreEqual($"curl {info.Version} ({info.Host}) libcurl/{info.Version} {info.TlsBackend}", lines[0]);
        string[] protocols = [.. info.Protocols, "ipfs", "ipns"];
        Array.Sort(protocols, StringComparer.Ordinal);
        Assert.AreEqual("Protocols: " + string.Join(' ', protocols), lines[2]);
        Assert.AreEqual("Features: " + string.Join(' ', info.Features), lines[3]);
    }
}
