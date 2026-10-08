using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the <see cref="NativeExtendedAttributeWriter" /> Linux and macOS get sets a real
/// extended attribute and reports the system's text when it cannot (ADR-0320). The Windows
/// case, which needs no file, stays in <c>NativeExtendedAttributeWriterTests</c>.
/// </summary>
[TestClass]
public sealed class NativeExtendedAttributeWriterIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void TryWrite_OnAFileOnLinuxOrMacOS_SetsTheAttribute()
    {
        string path = Path.Combine(Path.GetTempPath(), "bl651-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "hello");
        try
        {
            NativeExtendedAttributeWriter writer = NativeExtendedAttributeWriter.ForCurrentPlatform()!;
            Diagnostics.Arrange("file", "temporary file containing hello");
            Diagnostics.Arrange("attribute", OutputFileExtendedAttributes.MimeTypeName + " = text/plain");

            bool written = writer.TryWrite(path, OutputFileExtendedAttributes.MimeTypeName, "text/plain", out string errorText);
            Diagnostics.Act("written", written);
            Diagnostics.Act("error text", errorText);

            Diagnostics.Assert("written", true, written);
            Assert.IsTrue(written, errorText);
            Diagnostics.Assert("error text", string.Empty, errorText);
            Assert.AreEqual(string.Empty, errorText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void TryWrite_OnAMissingFile_FailsWithTheSystemText()
    {
        NativeExtendedAttributeWriter writer = NativeExtendedAttributeWriter.ForCurrentPlatform()!;
        Diagnostics.Arrange("path", "/nonexistent-bl651/x");
        Diagnostics.Arrange("attribute", OutputFileExtendedAttributes.CreatorName + " = curl");

        bool written = writer.TryWrite("/nonexistent-bl651/x", OutputFileExtendedAttributes.CreatorName, "curl", out string errorText);
        Diagnostics.Act("written", written);
        Diagnostics.Act("error text", errorText);

        Diagnostics.Assert("written", false, written);
        Assert.IsFalse(written);
        Diagnostics.Assert("error text", "No such file or directory", errorText);
        Assert.AreEqual("No such file or directory", errorText);
    }
}
