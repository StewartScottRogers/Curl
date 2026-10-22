using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins the upstream test data vendored under <c>UpstreamTestData/</c> (ADR-0013, decision 3):
/// every <c>tests/data/test*</c> file of curl 8.22.0, stored here as <c>test*.curltest</c>, and curl's
/// <c>COPYING</c> notice are
/// copied beside the tests, so no case ever has to download anything.
/// </summary>
[TestClass]
public sealed class UpstreamTestDataTests
{
    /// <summary>The number of <c>test*</c> files in <c>tests/data</c> at <c>curl-8_22_0</c>.</summary>
    private const int VendoredTestFileCount = 2072;

    /// <summary>The number of <c>*.md</c> files in <c>docs/cmdline-opts</c> at <c>curl-8_22_0</c>.</summary>
    private const int VendoredOptionDocCount = 298;

    /// <summary>The number of top-level <c>*.md</c> files in <c>docs/libcurl</c> at <c>curl-8_22_0</c>.</summary>
    private const int VendoredLibcurlDocCount = 104;

    /// <summary>The number of <c>*.md</c> files in <c>docs/libcurl/opts</c> at <c>curl-8_22_0</c>.</summary>
    private const int VendoredLibcurlOptionDocCount = 422;

    /// <summary>The number of <c>*.h</c> files in <c>include/curl</c> at <c>curl-8_22_0</c>.</summary>
    private const int VendoredHeaderCount = 12;

    private static readonly string UpstreamTestDataFolder = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_HoldsEveryTestFileFromTheTag()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pattern", "UpstreamTestData/test*.curltest");
        diagnostics.Arrange("expected file count", VendoredTestFileCount);

        string[] testFiles = Directory.GetFiles(UpstreamTestDataFolder, "test*.curltest");

        diagnostics.Act("file count", testFiles.Length);
        diagnostics.Assert("file count", VendoredTestFileCount, testFiles.Length);
        Assert.HasCount(VendoredTestFileCount, testFiles);
    }

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_HoldsTheFilesTheDocumentationChecksOpen()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("folder", "UpstreamTestData/docs and src");

        string[] required =
        [
            Path.Combine("docs", "options-in-versions"),
            Path.Combine("docs", "VERSIONS.md"),
            Path.Combine("docs", "cmdline-opts", "write-out.md"),
            Path.Combine("docs", "cmdline-opts", "verbose.md"),
            Path.Combine("src", "tool_writeout.c"),
        ];
        string[] missing = [.. required.Where(file => !File.Exists(Path.Combine(UpstreamTestDataFolder, file)))];
        int optionDocCount = Directory.GetFiles(Path.Combine(UpstreamTestDataFolder, "docs", "cmdline-opts"), "*.md").Length;

        diagnostics.Act("missing files", string.Join(", ", missing));
        diagnostics.Assert("missing files", 0, missing.Length);
        Assert.IsEmpty(missing);
        Assert.AreEqual(VendoredOptionDocCount, optionDocCount);
    }

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_HoldsTheLibcurlDocsAndHeadersTheTableChecksOpen()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("folder", "UpstreamTestData/docs/libcurl and include/curl");

        string[] required =
        [
            Path.Combine("docs", "libcurl", "symbols-in-versions"),
            Path.Combine("docs", "libcurl", "libcurl-errors.md"),
            Path.Combine("docs", "libcurl", "curl_easy_setopt.md"),
            Path.Combine("docs", "libcurl", "curl_version_info.md"),
            Path.Combine("docs", "libcurl", "opts", "CURLOPT_URL.md"),
            Path.Combine("include", "curl", "curl.h"),
            Path.Combine("src", "tool_getparam.c"),
            Path.Combine("src", "tool_listhelp.c"),
        ];
        string[] missing = [.. required.Where(file => !File.Exists(Path.Combine(UpstreamTestDataFolder, file)))];
        int libcurlDocCount = Directory.GetFiles(Path.Combine(UpstreamTestDataFolder, "docs", "libcurl"), "*.md").Length;
        int optionDocCount = Directory.GetFiles(Path.Combine(UpstreamTestDataFolder, "docs", "libcurl", "opts"), "*.md").Length;
        int headerCount = Directory.GetFiles(Path.Combine(UpstreamTestDataFolder, "include", "curl"), "*.h").Length;

        diagnostics.Act("missing files", string.Join(", ", missing));
        diagnostics.Assert("missing files", 0, missing.Length);
        Assert.IsEmpty(missing);
        Assert.AreEqual(VendoredLibcurlDocCount, libcurlDocCount);
        Assert.AreEqual(VendoredLibcurlOptionDocCount, optionDocCount);
        Assert.AreEqual(VendoredHeaderCount, headerCount);
    }

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_BesideTheSrcDirHoldsTheLibcurlDocsForPercentSrcDirParent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string besideSrcDir = Path.Combine(AppContext.BaseDirectory, "docs", "libcurl", "symbols-in-versions");
        string headerBesideSrcDir = Path.Combine(AppContext.BaseDirectory, "include", "curl", "curl.h");
        diagnostics.Arrange("folders", "docs/libcurl and include/curl beside UpstreamTestData");

        bool present = File.Exists(besideSrcDir) && File.Exists(headerBesideSrcDir);

        diagnostics.Act("both present", present);
        diagnostics.Assert("both present", true, present);
        Assert.IsTrue(present);
    }

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_HoldsTheXmlDocumentTheCompressedReplyCasesInclude()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string xmlPath = Path.Combine(UpstreamTestDataFolder, "data", "data-xml1");
        diagnostics.Arrange("file", "UpstreamTestData/data/data-xml1");

        byte[] xml = File.ReadAllBytes(xmlPath);

        diagnostics.Act("length in bytes", xml.Length);
        diagnostics.Assert("length in bytes", 5059, xml.Length);
        Assert.HasCount(5059, xml);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n", System.Text.Encoding.Latin1.GetString(xml));
    }

    [TestMethod]
    public void UpstreamTestData_CopiedBesideTheTests_CarriesCurlsCopyingNotice()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string copyingPath = Path.Combine(UpstreamTestDataFolder, "COPYING");
        diagnostics.Arrange("file", "UpstreamTestData/COPYING");

        string copying = File.ReadAllText(copyingPath);

        diagnostics.Act("length in characters", copying.Length);
        diagnostics.Assert("mentions Daniel Stenberg", true, copying.Contains("Daniel Stenberg", StringComparison.Ordinal));
        Assert.Contains("Daniel Stenberg", copying);
    }
}
