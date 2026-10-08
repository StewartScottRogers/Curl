using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Drives <see cref="NativeRegularFileTest" /> against the real file system off Windows, where it
/// calls the runtime's <c>stat</c> shim; on Windows it gives no test at all.
/// </summary>
[TestClass]
public sealed class NativeRegularFileTestTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ForCurrentPlatform_OnWindows_IsNull()
    {
        Diagnostics.Arrange("platform", "Windows");

        object? test = NativeRegularFileTest.ForCurrentPlatform();
        Diagnostics.Act("test is null", test is null);

        Diagnostics.Assert("test", null, test);
        Assert.IsNull(test);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsRegularFile_DevNull_IsFalse()
    {
        Diagnostics.Arrange("path", "/dev/null");

        bool result = NativeRegularFileTest.IsRegularFile("/dev/null");
        Diagnostics.Act("IsRegularFile", result);

        Diagnostics.Assert("IsRegularFile", false, result);
        Assert.IsFalse(NativeRegularFileTest.IsRegularFile("/dev/null"));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsRegularFile_Directory_IsFalse()
    {
        Diagnostics.Arrange("path", "the temporary directory");

        bool result = NativeRegularFileTest.IsRegularFile(Path.GetTempPath());
        Diagnostics.Act("IsRegularFile", result);

        Diagnostics.Assert("IsRegularFile", false, result);
        Assert.IsFalse(NativeRegularFileTest.IsRegularFile(Path.GetTempPath()));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsRegularFile_NothingThere_IsFalse()
    {
        string missing = Path.Combine(Path.GetTempPath(), "curl-missing-" + Guid.NewGuid().ToString("N"));
        Diagnostics.Arrange("path", "a missing file in the temporary directory");

        bool result = NativeRegularFileTest.IsRegularFile(missing);
        Diagnostics.Act("IsRegularFile", result);

        Diagnostics.Assert("IsRegularFile", false, result);
        Assert.IsFalse(NativeRegularFileTest.IsRegularFile(missing));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsRegularFile_RegularFile_IsTrue()
    {
        string file = Path.GetTempFileName();
        try
        {
            Diagnostics.Arrange("path", "a new empty temporary file");

            bool result = NativeRegularFileTest.IsRegularFile(file);
            Diagnostics.Act("IsRegularFile", result);

            Diagnostics.Assert("IsRegularFile", true, result);
            Assert.IsTrue(NativeRegularFileTest.IsRegularFile(file));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
