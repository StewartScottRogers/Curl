namespace Curl.Console;

/// <summary>
/// Pins, per operating system, which <see cref="NativeExtendedAttributeWriter" /> the composition
/// gets: none on Windows, where no curl build writes extended attributes, and one that sets a real
/// attribute on Linux and macOS (ADR-0320).
/// </summary>
[TestClass]
public sealed class NativeExtendedAttributeWriterTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ForCurrentPlatform_OnWindows_GivesNoWriter() =>
        Assert.IsNull(NativeExtendedAttributeWriter.ForCurrentPlatform());

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

            bool written = writer.TryWrite(path, OutputFileExtendedAttributes.MimeTypeName, "text/plain", out string errorText);

            Assert.IsTrue(written, errorText);
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

        bool written = writer.TryWrite("/nonexistent-bl651/x", OutputFileExtendedAttributes.CreatorName, "curl", out string errorText);

        Assert.IsFalse(written);
        Assert.AreEqual("No such file or directory", errorText);
    }
}
