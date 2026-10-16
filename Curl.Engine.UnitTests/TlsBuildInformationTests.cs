using Curl.Api.Options.Tls;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="TlsBuildInformation.Lines" />: no build-time engines for <c>--engine list</c>, nothing for
/// <c>--dump-ca-embed</c>, and no answer for an option group that asks neither (ADR-0151).
/// </summary>
[TestClass]
public sealed class TlsBuildInformationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Lines_EngineList_ListsNoEngines()
    {
        string[] lines = LinesFor(new EasyTlsOptions { Engine = "list" }, caEmbedDumpRequested: false)!.ToArray();

        Diagnostics.Assert("lines", "Build-time engines: |   <none>", string.Join(" | ", lines));
        CollectionAssert.AreEqual(
            new[] { "Build-time engines:", "  <none>" },
            lines);
    }

    [TestMethod]
    public void Lines_DumpCaEmbed_IsNoLines()
    {
        IReadOnlyList<string> lines = LinesFor(new EasyTlsOptions(), caEmbedDumpRequested: true)!;

        Diagnostics.Assert("line count", 0, lines.Count);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void Lines_NeitherOption_IsNull()
    {
        IReadOnlyList<string>? lines = LinesFor(new EasyTlsOptions { Engine = "pkcs11" }, caEmbedDumpRequested: false);

        Diagnostics.Assert("lines", "null", lines is null ? "null" : string.Join(" | ", lines));
        Assert.IsNull(lines);
    }

    private IReadOnlyList<string>? LinesFor(EasyTlsOptions tls, bool caEmbedDumpRequested)
    {
        Diagnostics.Arrange("engine", tls.Engine ?? "(none)");
        Diagnostics.Arrange("--dump-ca-embed", caEmbedDumpRequested);
        IReadOnlyList<string>? lines = TlsBuildInformation.Lines(tls, caEmbedDumpRequested);
        Diagnostics.Act("lines", lines is null ? "null" : string.Join(" | ", lines));
        return lines;
    }
}
