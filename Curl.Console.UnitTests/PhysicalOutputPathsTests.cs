using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Drives <see cref="PhysicalOutputPaths" /> against the real disk, in a fresh temporary
/// directory removed afterwards. None is <c>[TestCategory("Integration")]</c>: they need no
/// network, and the fast run must reach every line of <see cref="PhysicalOutputPaths" /> for its
/// coverage gate, as <c>PhysicalFileSystemTests</c> does for <c>PhysicalFileSystem</c>.
/// </summary>
[TestClass]
public sealed class PhysicalOutputPathsTests
{
    private readonly PhysicalOutputPaths outputPaths = new();

    // No regular-file test, as on Windows: every platform reaches the delete itself.
    private readonly PhysicalOutputPaths windowsOutputPaths = new(isRegularFile: null, CRuntimeErrorNumbers.Windows);
    private readonly string root = Path.Combine(Path.GetTempPath(), "curl-output-paths-" + Guid.NewGuid().ToString("N"));

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestInitialize]
    public void CreateRoot() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void TryCreateDirectory_Missing_CreatesIt()
    {
        Diagnostics.Arrange("directory", "<temporary root>/d, missing");
        string directory = Path.Combine(root, "d");

        bool created = outputPaths.TryCreateDirectory(directory, out int errorNumber);
        Diagnostics.Act("created", created);
        Diagnostics.Act("error number", errorNumber);

        Diagnostics.Assert("created / error number / exists", "True/0/True", $"{created}/{errorNumber}/{Directory.Exists(directory)}");
        Assert.IsTrue(created);
        Assert.AreEqual(0, errorNumber);
        Assert.IsTrue(Directory.Exists(directory));
    }

    [TestMethod]
    public void TryCreateDirectory_FileThere_IsTrueAndLeavesTheFile()
    {
        Diagnostics.Arrange("path", "<temporary root>/blk, a file");
        string file = Path.Combine(root, "blk");
        File.WriteAllText(file, "x");

        bool created = outputPaths.TryCreateDirectory(file, out _);
        Diagnostics.Act("created", created);

        Diagnostics.Assert("created / file still there", "True/True", $"{created}/{File.Exists(file)}");
        Assert.IsTrue(created);
        Assert.IsTrue(File.Exists(file));
    }

    [TestMethod]
    public void TryCreateDirectory_DirectoryThere_IsTrue()
    {
        Diagnostics.Arrange("directory", "<temporary root>, already there");

        bool created = outputPaths.TryCreateDirectory(root, out _);
        Diagnostics.Act("created", created);

        Diagnostics.Assert("created", true, created);
        Assert.IsTrue(created);
    }

    [TestMethod]
    public void TryCreateDirectory_UnderAFile_IsFalse()
    {
        Diagnostics.Arrange("directory", "<temporary root>/blk/b, where blk is a file");
        string file = Path.Combine(root, "blk");
        File.WriteAllText(file, "x");

        bool created = outputPaths.TryCreateDirectory(Path.Combine(file, "b"), out int errorNumber);
        Diagnostics.Act("created", created);
        Diagnostics.Act("error number is set", errorNumber != 0);

        Diagnostics.Assert("created / error number is set", "False/True", $"{created}/{errorNumber != 0}");
        Assert.IsFalse(created);
        Assert.AreNotEqual(0, errorNumber);
    }

    [TestMethod]
    public void Exists_FileThere_IsTrue()
    {
        Diagnostics.Arrange("path", "<temporary root>/f, a file");
        string file = Path.Combine(root, "f");
        File.WriteAllText(file, "x");

        bool exists = outputPaths.Exists(file);
        Diagnostics.Act("exists", exists);

        Diagnostics.Assert("exists", true, exists);
        Assert.IsTrue(exists);
    }

    [TestMethod]
    public void Exists_DirectoryThere_IsTrue()
    {
        Diagnostics.Arrange("path", "<temporary root>, a directory");

        bool exists = outputPaths.Exists(root);
        Diagnostics.Act("exists", exists);

        Diagnostics.Assert("exists", true, exists);
        Assert.IsTrue(exists);
    }

    [TestMethod]
    public void Exists_NothingThere_IsFalse()
    {
        Diagnostics.Arrange("path", "<temporary root>/missing");

        bool exists = outputPaths.Exists(Path.Combine(root, "missing"));
        Diagnostics.Act("exists", exists);

        Diagnostics.Assert("exists", false, exists);
        Assert.IsFalse(exists);
    }

    [TestMethod]
    public void RemoveFile_FileThere_RemovesIt()
    {
        Diagnostics.Arrange("path", "<temporary root>/f, a file; no regular-file test");
        string file = Path.Combine(root, "f");
        File.WriteAllText(file, "x");

        OutputFileRemoval removal = windowsOutputPaths.RemoveFile(file);
        Diagnostics.Act("removal", removal);

        Diagnostics.Assert("removal / file still there", "Removed/False", $"{removal}/{File.Exists(file)}");
        Assert.AreEqual(OutputFileRemoval.Removed, removal);
        Assert.IsFalse(File.Exists(file));
    }

    [TestMethod]
    public void RemoveFile_NothingThereNoRegularFileTest_Fails()
    {
        Diagnostics.Arrange("path", "<temporary root>/missing; no regular-file test");

        OutputFileRemoval removal = windowsOutputPaths.RemoveFile(Path.Combine(root, "missing"));
        Diagnostics.Act("removal", removal);

        Diagnostics.Assert("removal", OutputFileRemoval.Failed, removal);
        Assert.AreEqual(OutputFileRemoval.Failed, removal);
    }

    [TestMethod]
    public void RemoveFile_DirectoryNoRegularFileTest_FailsAndLeavesIt()
    {
        Diagnostics.Arrange("path", "<temporary root>, a directory; no regular-file test");

        OutputFileRemoval removal = windowsOutputPaths.RemoveFile(root);
        Diagnostics.Act("removal", removal);

        Diagnostics.Assert("removal / directory still there", "Failed/True", $"{removal}/{Directory.Exists(root)}");
        Assert.AreEqual(OutputFileRemoval.Failed, removal);
        Assert.IsTrue(Directory.Exists(root));
    }

    [TestMethod]
    public void RemoveFile_RegularFileTestSaysNotRegular_IsNotRegularFileAndLeavesIt()
    {
        Diagnostics.Arrange("path", "<temporary root>/dev, a file the regular-file test calls not regular");
        string file = Path.Combine(root, "dev");
        File.WriteAllText(file, "x");
        PhysicalOutputPaths outputPathsSeeingADevice = new(path => path != file, CRuntimeErrorNumbers.Linux);

        OutputFileRemoval removal = outputPathsSeeingADevice.RemoveFile(file);
        Diagnostics.Act("removal", removal);

        Diagnostics.Assert("removal / file still there", "NotRegularFile/True", $"{removal}/{File.Exists(file)}");
        Assert.AreEqual(OutputFileRemoval.NotRegularFile, removal);
        Assert.IsTrue(File.Exists(file));
    }

    [TestMethod]
    public void RemoveFile_RegularFileTestSaysRegular_RemovesIt()
    {
        Diagnostics.Arrange("path", "<temporary root>/f, a file the regular-file test calls regular");
        string file = Path.Combine(root, "f");
        File.WriteAllText(file, "x");
        PhysicalOutputPaths outputPathsSeeingAFile = new(path => path == file, CRuntimeErrorNumbers.Linux);

        OutputFileRemoval removal = outputPathsSeeingAFile.RemoveFile(file);
        Diagnostics.Act("removal", removal);

        Diagnostics.Assert("removal / file still there", "Removed/False", $"{removal}/{File.Exists(file)}");
        Assert.AreEqual(OutputFileRemoval.Removed, removal);
        Assert.IsFalse(File.Exists(file));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void RemoveFile_DevNullOffWindows_IsNotRegularFile()
    {
        Diagnostics.Arrange("path", "/dev/null");

        OutputFileRemoval removal = outputPaths.RemoveFile("/dev/null");
        Diagnostics.Act("removal", removal);

        Diagnostics.Assert("removal", OutputFileRemoval.NotRegularFile, removal);
        Assert.AreEqual(OutputFileRemoval.NotRegularFile, removal);
    }

    // Windows refuses to delete a file another handle holds open without FILE_SHARE_DELETE;
    // elsewhere the unlink succeeds, so the refusal is only reachable there.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RemoveFile_FileHeldOpen_FailsAndLeavesIt()
    {
        Diagnostics.Arrange("path", "<temporary root>/held, held open without delete sharing");
        string file = Path.Combine(root, "held");
        File.WriteAllText(file, "x");

        OutputFileRemoval removal;
        using (File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            removal = windowsOutputPaths.RemoveFile(file);
        }

        Diagnostics.Act("removal", removal);
        Diagnostics.Assert("removal / file still there", "Failed/True", $"{removal}/{File.Exists(file)}");
        Assert.AreEqual(OutputFileRemoval.Failed, removal);
        Assert.IsTrue(File.Exists(file));
    }

    [TestMethod]
    public void IsDeleteFailure_TheExceptionsFileDeleteRaises_AreTrue()
    {
        Diagnostics.Arrange("exceptions", "IOException, UnauthorizedAccessException");

        bool io = PhysicalOutputPaths.IsDeleteFailure(new IOException());
        bool access = PhysicalOutputPaths.IsDeleteFailure(new UnauthorizedAccessException());
        Diagnostics.Act("delete failures", $"{io}/{access}");

        Diagnostics.Assert("delete failures", "True/True", $"{io}/{access}");
        Assert.IsTrue(io);
        Assert.IsTrue(access);
    }

    [TestMethod]
    public void IsDeleteFailure_AnyOtherException_IsFalse()
    {
        Diagnostics.Arrange("exception", nameof(InvalidOperationException));

        bool deleteFailure = PhysicalOutputPaths.IsDeleteFailure(new InvalidOperationException());
        Diagnostics.Act("delete failure", deleteFailure);

        Diagnostics.Assert("delete failure", false, deleteFailure);
        Assert.IsFalse(deleteFailure);
    }

    [TestMethod]
    public void IsCreateFailure_TheExceptionsCreateDirectoryRaises_AreTrue()
    {
        Diagnostics.Arrange("exceptions", "IOException, UnauthorizedAccessException, ArgumentException, NotSupportedException");

        bool io = PhysicalOutputPaths.IsCreateFailure(new IOException());
        bool access = PhysicalOutputPaths.IsCreateFailure(new UnauthorizedAccessException());
        bool argument = PhysicalOutputPaths.IsCreateFailure(new ArgumentException());
        bool notSupported = PhysicalOutputPaths.IsCreateFailure(new NotSupportedException());
        Diagnostics.Act("create failures", $"{io}/{access}/{argument}/{notSupported}");

        Diagnostics.Assert("create failures", "True/True/True/True", $"{io}/{access}/{argument}/{notSupported}");
        Assert.IsTrue(io);
        Assert.IsTrue(access);
        Assert.IsTrue(argument);
        Assert.IsTrue(notSupported);
    }

    [TestMethod]
    public void IsCreateFailure_AnyOtherException_IsFalse()
    {
        Diagnostics.Arrange("exception", nameof(InvalidOperationException));

        bool createFailure = PhysicalOutputPaths.IsCreateFailure(new InvalidOperationException());
        Diagnostics.Act("create failure", createFailure);

        Diagnostics.Assert("create failure", false, createFailure);
        Assert.IsFalse(createFailure);
    }

    [TestMethod]
    public void ErrorNumberOf_UnauthorizedAccess_IsPermissionDenied()
    {
        Diagnostics.Arrange("exception / platform", "UnauthorizedAccessException / Linux");

        int errorNumber = PhysicalOutputPaths.ErrorNumberOf(new UnauthorizedAccessException(), CRuntimeErrorNumbers.Linux);
        Diagnostics.Act("error number", errorNumber);

        Diagnostics.Assert("error number", 13, errorNumber);
        Assert.AreEqual(13, errorNumber);
    }

    [TestMethod]
    public void ErrorNumberOf_PathTooLong_IsThePlatformsNameTooLong()
    {
        Diagnostics.Arrange("exception / platform", "PathTooLongException / macOS");

        int errorNumber = PhysicalOutputPaths.ErrorNumberOf(new PathTooLongException(), CRuntimeErrorNumbers.MacOS);
        Diagnostics.Act("error number", errorNumber);

        Diagnostics.Assert("error number", 63, errorNumber);
        Assert.AreEqual(63, errorNumber);
    }

    [TestMethod]
    public void ErrorNumberOf_MissingParent_IsNoSuchFileOrDirectory()
    {
        Diagnostics.Arrange("exceptions / platform", "DirectoryNotFoundException, FileNotFoundException / Linux");

        int directory = PhysicalOutputPaths.ErrorNumberOf(new DirectoryNotFoundException(), CRuntimeErrorNumbers.Linux);
        int file = PhysicalOutputPaths.ErrorNumberOf(new FileNotFoundException(), CRuntimeErrorNumbers.Linux);
        Diagnostics.Act("error numbers", $"{directory}/{file}");

        Diagnostics.Assert("error numbers", "2/2", $"{directory}/{file}");
        Assert.AreEqual(2, directory);
        Assert.AreEqual(2, file);
    }

    /// <summary>
    /// Off Windows .NET sets an <see cref="IOException" />'s <see cref="Exception.HResult" /> to the
    /// raw <c>errno</c> it has no type for; a Win32 code is mapped as the Windows C runtime maps it.
    /// </summary>
    /// <param name="hresult">The exception's HResult.</param>
    /// <param name="expected">The errno.</param>
    [TestMethod]
    [DataRow(30, 30)]
    [DataRow(122, 122)]
    [DataRow(unchecked((int)0x80070070), 28)]
    [DataRow(unchecked((int)0x80070050), 17)]
    [DataRow(unchecked((int)0x800700B7), 17)]
    [DataRow(unchecked((int)0x80070057), 22)]
    public void ErrorNumberOf_IOException_ReadsItsHResult(int hresult, int expected)
    {
        Diagnostics.Arrange("HResult", $"0x{hresult:X8}");

        int errorNumber = PhysicalOutputPaths.ErrorNumberOf(new IOException("x", hresult), CRuntimeErrorNumbers.Linux);
        Diagnostics.Act("error number", errorNumber);

        Diagnostics.Assert("error number", expected, errorNumber);
        Assert.AreEqual(expected, errorNumber);
    }
}
