using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Drives <see cref="NativeInheritedFileDescriptor" /> against real descriptors off Windows, where
/// it calls the runtime's <c>fcntl</c> shim.
/// </summary>
[TestClass]
public sealed class NativeInheritedFileDescriptorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsInherited_ADescriptorTheRuntimeOpened_IsFalse()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            using FileStream file = new(path, FileMode.CreateNew);
            int descriptor = (int)file.SafeFileHandle.DangerousGetHandle();
            Diagnostics.Arrange("descriptor", descriptor);

            bool inherited = NativeInheritedFileDescriptor.IsInherited(descriptor);
            Diagnostics.Act("inherited", inherited);

            Diagnostics.Assert("inherited", false, inherited);
            Assert.IsFalse(inherited);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsInherited_ADescriptorThatIsNotOpen_IsFalse()
    {
        Diagnostics.Arrange("descriptor", -1);

        bool inherited = NativeInheritedFileDescriptor.IsInherited(-1);
        Diagnostics.Act("inherited", inherited);

        Diagnostics.Assert("inherited", false, inherited);
        Assert.IsFalse(inherited);
    }
}
