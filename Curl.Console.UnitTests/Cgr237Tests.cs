using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-237: bytes of a body already received reach the <c>-o</c> file at once, so a process killed
/// mid-transfer leaves them there, as curl 8.22.0 does for fewer than 4 KiB.
/// </summary>
[TestClass]
public sealed class Cgr237Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_UnderFourKiB_ReachesTheFileBeforeTheStreamIsClosed()
    {
        Diagnostics.Arrange("write", "100 bytes to a -o file, stream left open");
        string directory = Directory.CreateTempSubdirectory("cgr237").FullName;
        try
        {
            string path = System.IO.Path.Combine(directory, "out.bin");
            await using DeferredOutputFileStream stream = new(new PhysicalFileSystem(), path, FileWriteMode.Truncate);

            await stream.WriteAsync(new byte[100].AsMemory(), TestContext.CancellationToken);
            long onDisk;
            using (FileStream reader = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                onDisk = reader.Length;
            }

            Diagnostics.Assert("bytes on disk", "100", onDisk.ToString());
            Assert.AreEqual(100L, onDisk);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
