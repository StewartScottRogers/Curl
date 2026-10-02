namespace Curl.Console;

/// <summary>
/// Drives <see cref="NativeRegularFileTest" /> against the real file system off Windows, where it
/// calls the runtime's <c>stat</c> shim; on Windows it gives no test at all.
/// </summary>
[TestClass]
public sealed class NativeRegularFileTestTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ForCurrentPlatform_OnWindows_IsNull() => Assert.IsNull(NativeRegularFileTest.ForCurrentPlatform());

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsRegularFile_DevNull_IsFalse() => Assert.IsFalse(NativeRegularFileTest.IsRegularFile("/dev/null"));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsRegularFile_Directory_IsFalse() => Assert.IsFalse(NativeRegularFileTest.IsRegularFile(Path.GetTempPath()));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsRegularFile_NothingThere_IsFalse() =>
        Assert.IsFalse(NativeRegularFileTest.IsRegularFile(Path.Combine(Path.GetTempPath(), "curl-missing-" + Guid.NewGuid().ToString("N"))));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsRegularFile_RegularFile_IsTrue()
    {
        string file = Path.GetTempFileName();
        try
        {
            Assert.IsTrue(NativeRegularFileTest.IsRegularFile(file));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
