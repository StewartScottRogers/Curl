using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that Windows, where no curl build writes extended attributes, gets no
/// <see cref="NativeExtendedAttributeWriter" /> (ADR-0320). The Linux and macOS writer, which sets
/// a real attribute, is pinned in <c>NativeExtendedAttributeWriterIntegrationTests</c>.
/// </summary>
[TestClass]
public sealed class NativeExtendedAttributeWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ForCurrentPlatform_OnWindows_GivesNoWriter()
    {
        Diagnostics.Arrange("platform", "Windows");

        NativeExtendedAttributeWriter? writer = NativeExtendedAttributeWriter.ForCurrentPlatform();
        Diagnostics.Act("writer is null", writer is null);

        Diagnostics.Assert("writer", null, writer);
        Assert.IsNull(writer);
    }
}
