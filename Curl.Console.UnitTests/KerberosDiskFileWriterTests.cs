using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="KerberosDiskFileWriter" /> against a temporary directory: bytes appended to
/// an existing file, and a missing file or a directory left as it is. None is
/// <c>[TestCategory("Integration")]</c>: they need only the temporary directory, removed afterwards.
/// </summary>
[TestClass]
public sealed class KerberosDiskFileWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void AppendAllBytes_ExistingFile_AppendsAfterItsBytes()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(directory, "krb5cc_1000");
            File.WriteAllBytes(path, [0x05, 0x04]);
            Diagnostics.Arrange("file name", "krb5cc_1000");
            Diagnostics.Bytes("existing content", new byte[] { 0x05, 0x04 });
            Diagnostics.Bytes("appended bytes", new byte[] { 0x01, 0x02 });

            bool appended = new KerberosDiskFileWriter().AppendAllBytes(path, [0x01, 0x02]);
            byte[] content = File.ReadAllBytes(path);

            Diagnostics.Act("appended", appended);
            Diagnostics.Bytes("content after", content);

            Diagnostics.Assert("appended", true, appended);
            Assert.IsTrue(appended);
            Diagnostics.Diff("content after", new byte[] { 0x05, 0x04, 0x01, 0x02 }, content);
            CollectionAssert.AreEqual(new byte[] { 0x05, 0x04, 0x01, 0x02 }, content);
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
            Diagnostics.Arrange("file name", "krb5cc_1000 (missing)");

            bool appended = new KerberosDiskFileWriter().AppendAllBytes(path, [0x01]);
            bool exists = File.Exists(path);

            Diagnostics.Act("appended", appended);
            Diagnostics.Act("file exists", exists);

            Diagnostics.Assert("appended", false, appended);
            Assert.IsFalse(appended);
            Diagnostics.Assert("file exists", false, exists);
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
            Diagnostics.Arrange("path", "an existing directory");

            bool appended = new KerberosDiskFileWriter().AppendAllBytes(directory, [0x01]);

            Diagnostics.Act("appended", appended);

            Diagnostics.Assert("appended", false, appended);
            Assert.IsFalse(appended);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
