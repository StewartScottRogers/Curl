using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.FileSystem;

/// <summary>
/// Drives <see cref="PhysicalFileSystem" /> against the operating system's real null
/// device, pinning what curl 8.21.0 was measured to report for it.
/// </summary>
[TestClass]
public sealed class PhysicalFileSystemIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    // curl 8.21.0 on Windows, measured: `curl -sI file:///NUL` prints
    // "Content-Length: 0", "Accept-ranges: bytes" and "Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT".
    // FileProtocolHandler writes the date with the "R" format, so this pins its bytes.
    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForReadAsync_WindowsNullDevice_ReportsCurlsMeasuredLastModifiedDate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", "NUL");

        var result = await new PhysicalFileSystem().OpenForReadAsync("NUL", CancellationToken.None);

        ActResult(diagnostics, result);
        await result.Content!.DisposeAsync();
        diagnostics.Assert("length", 0L, result.Length);
        Assert.AreEqual(0L, result.Length);
        Assert.IsNotNull(result.LastWriteTimeUtc);
        string lastModified = result.LastWriteTimeUtc.Value.UtcDateTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        diagnostics.Assert("last modified", "Thu, 01 Jan 1970 00:00:00 GMT", lastModified);
        Assert.AreEqual(
            "Thu, 01 Jan 1970 00:00:00 GMT",
            lastModified);
    }

    private static void ActResult(TestDiagnostics diagnostics, FileOpenResult result)
    {
        diagnostics.Act("status", result.Status);
        diagnostics.Act("content", result.Content is null ? "none" : "open");
        diagnostics.Act("length", result.Length);
        diagnostics.Act("failure exception", result.FailureException?.GetType().Name ?? "none");
    }
}
