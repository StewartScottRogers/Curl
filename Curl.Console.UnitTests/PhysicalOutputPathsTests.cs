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
    private readonly PhysicalOutputPaths windowsOutputPaths = new(isRegularFile: null);
    private readonly string root = Path.Combine(Path.GetTempPath(), "curl-output-paths-" + Guid.NewGuid().ToString("N"));

    [TestInitialize]
    public void CreateRoot() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void TryCreateDirectory_Missing_CreatesIt()
    {
        string directory = Path.Combine(root, "d");

        Assert.IsTrue(outputPaths.TryCreateDirectory(directory));
        Assert.IsTrue(Directory.Exists(directory));
    }

    [TestMethod]
    public void TryCreateDirectory_FileThere_IsTrueAndLeavesTheFile()
    {
        string file = Path.Combine(root, "blk");
        File.WriteAllText(file, "x");

        Assert.IsTrue(outputPaths.TryCreateDirectory(file));
        Assert.IsTrue(File.Exists(file));
    }

    [TestMethod]
    public void TryCreateDirectory_DirectoryThere_IsTrue() => Assert.IsTrue(outputPaths.TryCreateDirectory(root));

    [TestMethod]
    public void TryCreateDirectory_UnderAFile_IsFalse()
    {
        string file = Path.Combine(root, "blk");
        File.WriteAllText(file, "x");

        Assert.IsFalse(outputPaths.TryCreateDirectory(Path.Combine(file, "b")));
    }

    [TestMethod]
    public void Exists_FileThere_IsTrue()
    {
        string file = Path.Combine(root, "f");
        File.WriteAllText(file, "x");

        Assert.IsTrue(outputPaths.Exists(file));
    }

    [TestMethod]
    public void Exists_DirectoryThere_IsTrue() => Assert.IsTrue(outputPaths.Exists(root));

    [TestMethod]
    public void Exists_NothingThere_IsFalse() => Assert.IsFalse(outputPaths.Exists(Path.Combine(root, "missing")));

    [TestMethod]
    public void RemoveFile_FileThere_RemovesIt()
    {
        string file = Path.Combine(root, "f");
        File.WriteAllText(file, "x");

        Assert.AreEqual(OutputFileRemoval.Removed, windowsOutputPaths.RemoveFile(file));
        Assert.IsFalse(File.Exists(file));
    }

    [TestMethod]
    public void RemoveFile_NothingThereNoRegularFileTest_Fails() => Assert.AreEqual(OutputFileRemoval.Failed, windowsOutputPaths.RemoveFile(Path.Combine(root, "missing")));

    [TestMethod]
    public void RemoveFile_DirectoryNoRegularFileTest_FailsAndLeavesIt()
    {
        Assert.AreEqual(OutputFileRemoval.Failed, windowsOutputPaths.RemoveFile(root));
        Assert.IsTrue(Directory.Exists(root));
    }

    [TestMethod]
    public void RemoveFile_RegularFileTestSaysNotRegular_IsNotRegularFileAndLeavesIt()
    {
        string file = Path.Combine(root, "dev");
        File.WriteAllText(file, "x");
        PhysicalOutputPaths outputPathsSeeingADevice = new(path => path != file);

        Assert.AreEqual(OutputFileRemoval.NotRegularFile, outputPathsSeeingADevice.RemoveFile(file));
        Assert.IsTrue(File.Exists(file));
    }

    [TestMethod]
    public void RemoveFile_RegularFileTestSaysRegular_RemovesIt()
    {
        string file = Path.Combine(root, "f");
        File.WriteAllText(file, "x");
        PhysicalOutputPaths outputPathsSeeingAFile = new(path => path == file);

        Assert.AreEqual(OutputFileRemoval.Removed, outputPathsSeeingAFile.RemoveFile(file));
        Assert.IsFalse(File.Exists(file));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void RemoveFile_DevNullOffWindows_IsNotRegularFile() =>
        Assert.AreEqual(OutputFileRemoval.NotRegularFile, outputPaths.RemoveFile("/dev/null"));

    // Windows refuses to delete a file another handle holds open without FILE_SHARE_DELETE;
    // elsewhere the unlink succeeds, so the refusal is only reachable there.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void RemoveFile_FileHeldOpen_FailsAndLeavesIt()
    {
        string file = Path.Combine(root, "held");
        File.WriteAllText(file, "x");

        OutputFileRemoval removal;
        using (File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            removal = windowsOutputPaths.RemoveFile(file);
        }

        Assert.AreEqual(OutputFileRemoval.Failed, removal);
        Assert.IsTrue(File.Exists(file));
    }

    [TestMethod]
    public void IsDeleteFailure_TheExceptionsFileDeleteRaises_AreTrue()
    {
        Assert.IsTrue(PhysicalOutputPaths.IsDeleteFailure(new IOException()));
        Assert.IsTrue(PhysicalOutputPaths.IsDeleteFailure(new UnauthorizedAccessException()));
    }

    [TestMethod]
    public void IsDeleteFailure_AnyOtherException_IsFalse() =>
        Assert.IsFalse(PhysicalOutputPaths.IsDeleteFailure(new InvalidOperationException()));

    [TestMethod]
    public void IsCreateFailure_TheExceptionsCreateDirectoryRaises_AreTrue()
    {
        Assert.IsTrue(PhysicalOutputPaths.IsCreateFailure(new IOException()));
        Assert.IsTrue(PhysicalOutputPaths.IsCreateFailure(new UnauthorizedAccessException()));
        Assert.IsTrue(PhysicalOutputPaths.IsCreateFailure(new ArgumentException()));
        Assert.IsTrue(PhysicalOutputPaths.IsCreateFailure(new NotSupportedException()));
    }

    [TestMethod]
    public void IsCreateFailure_AnyOtherException_IsFalse() =>
        Assert.IsFalse(PhysicalOutputPaths.IsCreateFailure(new InvalidOperationException()));
}
