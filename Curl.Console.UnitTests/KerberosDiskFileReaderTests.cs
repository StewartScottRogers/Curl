namespace Curl.Console;

/// <summary>
/// Pins <see cref="KerberosDiskFileReader" /> against a temporary directory: a file's bytes, a
/// directory's file names, and absence for what is not there. None is
/// <c>[TestCategory("Integration")]</c>: they need only the temporary directory, removed afterwards.
/// </summary>
[TestClass]
public sealed class KerberosDiskFileReaderTests
{
    [TestMethod]
    public void ReadAllBytes_ExistingFile_ReturnsItsBytes()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(directory, "krb5cc_1000");
            File.WriteAllBytes(path, [0x05, 0x04]);

            CollectionAssert.AreEqual(new byte[] { 0x05, 0x04 }, new KerberosDiskFileReader().ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadAllBytes_MissingFile_ReturnsNull()
    {
        Assert.IsNull(new KerberosDiskFileReader().ReadAllBytes(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "krb5.conf")));
    }

    [TestMethod]
    public void ReadAllBytes_Directory_ReturnsNull()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.IsNull(new KerberosDiskFileReader().ReadAllBytes(directory));
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

            CollectionAssert.AreEqual(new[] { "a.conf" }, new KerberosDiskFileReader().ListFileNames(directory)!.ToArray());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ListFileNames_MissingDirectory_ReturnsNull()
    {
        Assert.IsNull(new KerberosDiskFileReader().ListFileNames(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}
