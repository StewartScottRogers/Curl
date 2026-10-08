using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="KerberosDiskFileReader" /> against a temporary directory: a file's bytes, a
/// directory's file names, and absence for what is not there. None is
/// <c>[TestCategory("Integration")]</c>: they need only the temporary directory, removed afterwards.
/// </summary>
[TestClass]
public sealed class KerberosDiskFileReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReadAllBytes_ExistingFile_ReturnsItsBytes()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(directory, "krb5cc_1000");
            File.WriteAllBytes(path, [0x05, 0x04]);
            Diagnostics.Arrange("file name", "krb5cc_1000");
            Diagnostics.Bytes("file content", new byte[] { 0x05, 0x04 });

            byte[]? actual = new KerberosDiskFileReader().ReadAllBytes(path);

            Diagnostics.Act("read bytes", actual is null ? "null" : Convert.ToHexString(actual));

            Diagnostics.Diff("read bytes", new byte[] { 0x05, 0x04 }, actual ?? []);
            CollectionAssert.AreEqual(new byte[] { 0x05, 0x04 }, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadAllBytes_MissingFile_ReturnsNull()
    {
        Diagnostics.Arrange("file", "random missing krb5.conf");

        byte[]? actual = new KerberosDiskFileReader().ReadAllBytes(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "krb5.conf"));

        Diagnostics.Act("read bytes", actual is null ? "null" : "not null");

        Diagnostics.Assert("read bytes is null", true, actual is null);
        Assert.IsNull(actual);
    }

    [TestMethod]
    public void ReadAllBytes_Directory_ReturnsNull()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Diagnostics.Arrange("path", "an existing directory");

            byte[]? actual = new KerberosDiskFileReader().ReadAllBytes(directory);

            Diagnostics.Act("read bytes", actual is null ? "null" : "not null");

            Diagnostics.Assert("read bytes is null", true, actual is null);
            Assert.IsNull(actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ListFileNames_ExistingDirectory_ReturnsItsFileNamesOnly()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "a.conf"), string.Empty);
            Directory.CreateDirectory(Path.Combine(directory, "sub"));
            Diagnostics.Arrange("directory contents", "a.conf (file), sub (directory)");

            string[] actual = new KerberosDiskFileReader().ListFileNames(directory)!.ToArray();

            Diagnostics.Act("file names", string.Join(",", actual));

            Diagnostics.Assert("file names", "a.conf", string.Join(",", actual));
            CollectionAssert.AreEqual(new[] { "a.conf" }, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ListFileNames_MissingDirectory_ReturnsNull()
    {
        Diagnostics.Arrange("directory", "random missing directory");

        var names = new KerberosDiskFileReader().ListFileNames(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Diagnostics.Act("file names", names is null ? "null" : "not null");

        Diagnostics.Assert("file names is null", true, names is null);
        Assert.IsNull(names);
    }
}
