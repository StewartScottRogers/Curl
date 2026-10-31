using Curl.Extension.WebDavTools.Sync;

namespace Curl.Extension.WebDavTools.UnitTests.Sync;

[TestClass]
public sealed class WebDavDiskFileSystemTests
{
    [TestMethod]
    public void FindFile_ExistingFile_ReturnsItsLengthAndLastWriteTime()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(folder.FullName, "a.txt");
            File.WriteAllBytes(path, [1, 2, 3]);
            var lastWrite = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, lastWrite);

            var file = new WebDavDiskFileSystem().FindFile(path);

            Assert.AreEqual(new WebDavLocalFile(3, new DateTimeOffset(lastWrite)), file);
        }
        finally
        {
            folder.Delete(true);
        }
    }

    [TestMethod]
    public void FindFile_MissingFile_ReturnsNull()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            Assert.IsNull(new WebDavDiskFileSystem().FindFile(Path.Combine(folder.FullName, "missing.txt")));
        }
        finally
        {
            folder.Delete(true);
        }
    }
}
