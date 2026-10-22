using System.Text;
using Curl.Console;

namespace Curl.Conformance;

// The CURL_FAKE_XATTR stand-in of UpstreamConformanceTests.FakeXattrStandIn.cs (BL-2833).
[TestClass]
public sealed class FakeXattrStandInTests
{
    private const string CaseClient = "<client>\n<server>\nhttp\n</server>\n<features>\nDebug\nxattr\n</features>\n";

    [TestMethod]
    public void PlatformFor_CaseSetsFakeXattr_ReportsDebug()
    {
        byte[] testFile = Encoding.Latin1.GetBytes(CaseClient + "<setenv>\nCURL_FAKE_XATTR=1\n</setenv>\n</client>\n");

        UpstreamCurlPlatform platform = UpstreamConformanceTests.PlatformFor(testFile);

        Assert.IsTrue(platform.Features.Contains("Debug"));
    }

    [TestMethod]
    public void PlatformFor_CaseSetsNoFakeXattr_ReportsNoDebug()
    {
        byte[] testFile = Encoding.Latin1.GetBytes(CaseClient + "<setenv>\nCURL_OTHER=1\n</setenv>\n</client>\n");

        UpstreamCurlPlatform platform = UpstreamConformanceTests.PlatformFor(testFile);

        Assert.IsFalse(platform.Features.Contains("Debug"));
    }

    // test688's <stdout> off Windows, which tool_xattr.c's debug build prints as "%s => %s\n" per attribute.
    [TestMethod]
    public void TryWrite_Test688Attributes_PrintsUpstreamsFakeLines()
    {
        using MemoryStream standardOutput = new();
        UpstreamConformanceTests.FakeExtendedAttributeWriter writer = new(standardOutput);
        IReadOnlyList<KeyValuePair<string, string>> attributes = OutputFileExtendedAttributes.For(
            "http://127.0.0.1:8990/688", "https://referrer.invalid/", "fake/data");

        string? warning = OutputFileExtendedAttributes.Write(writer, "log/688", attributes);

        Assert.IsNull(warning);
        Assert.AreEqual(
            "user.creator => curl\n"
            + "user.xdg.referrer.url => https://referrer.invalid/\n"
            + "user.mime_type => fake/data\n"
            + "user.xdg.origin.url => http://127.0.0.1:8990/688\n",
            Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public void TryWrite_AnyAttribute_ReportsItSetWithNoErrorText()
    {
        using MemoryStream standardOutput = new();
        UpstreamConformanceTests.FakeExtendedAttributeWriter writer = new(standardOutput);

        bool written = writer.TryWrite("log/687", "user.mime_type", "fake/data", out string errorText);

        Assert.IsTrue(written);
        Assert.IsEmpty(errorText);
    }
}
