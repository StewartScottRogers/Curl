using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-174 of the 2026-10-25 gap report: a file open for writing can be opened for
/// writing again while the first stream is open, as curl's <c>fopen</c> shares it, so two
/// <c>-Z</c> transfers with the same <c>-o</c> file both write it. curl 8.22.0 (Schannel)
/// exited 0 for both; the port's second open failed on Windows with exit 23.
/// </summary>
[TestClass]
public sealed class Cgr174Tests
{
    [TestMethod]
    public async Task OpenForWriteAsync_FileAlreadyOpenForWriting_OpensItAgainAndBothWritesLand()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cgr174-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "same.txt");
            var fileSystem = new PhysicalFileSystem();

            FileOpenResult first = await fileSystem.OpenForWriteAsync(
                path, FileWriteMode.Truncate, TransferContext.DefaultCreateFileMode, CancellationToken.None);
            FileOpenResult second = await fileSystem.OpenForWriteAsync(
                path, FileWriteMode.Truncate, TransferContext.DefaultCreateFileMode, CancellationToken.None);

            Assert.AreEqual(FileAccessStatus.Ok, first.Status);
            Assert.AreEqual(FileAccessStatus.Ok, second.Status);
            await using (Stream firstStream = first.Content!)
            await using (Stream secondStream = second.Content!)
            {
                await firstStream.WriteAsync("ab"u8.ToArray());
                await firstStream.FlushAsync();
                await secondStream.WriteAsync("c"u8.ToArray());
                await secondStream.FlushAsync();
            }

            Assert.AreEqual("cb", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
