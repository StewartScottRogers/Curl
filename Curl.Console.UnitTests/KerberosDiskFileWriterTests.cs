namespace Curl.Console;

/// <summary>
/// Pins <see cref="KerberosDiskFileWriter" /> against a temporary directory: bytes appended to
/// an existing file, and a missing file or a directory left as it is. None is
/// <c>[TestCategory("Integration")]</c>: they need only the temporary directory, removed afterwards.
/// </summary>
[TestClass]
public sealed class KerberosDiskFileWriterTests
{
    [TestMethod]
    public void AppendAllBytes_ExistingFile_AppendsAfterItsBytes()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(directory, "krb5cc_1000");
            File.WriteAllBytes(path, [0x05, 0x04]);

            bool appended = new KerberosDiskFileWriter().AppendAllBytes(path, [0x01, 0x02]);

            Assert.IsTrue(appended);
            CollectionAssert.AreEqual(new byte[] { 0x05, 0x04, 0x01, 0x02 }, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void AppendAllBytes_MissingFile_ReturnsFalseAndCreatesNothing()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(directory, "krb5cc_1000");

            bool appended = new KerberosDiskFileWriter().AppendAllBytes(path, [0x01]);

            Assert.IsFalse(appended);
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void AppendAllBytes_Directory_ReturnsFalse()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.IsFalse(new KerberosDiskFileWriter().AppendAllBytes(directory, [0x01]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
