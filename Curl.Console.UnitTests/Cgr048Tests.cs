using Curl.Engine;

namespace Curl.Console;

/// <summary>
/// CGR-048 (BL-3577): <c>--create-dirs -o blk/b/c.txt</c> with a regular file <c>blk</c> in the
/// way. curl's <c>mkdir("blk/b")</c> fails with <c>ENOENT</c> on Windows and <c>ENOTDIR</c>
/// elsewhere, so it prints <c>curl: Error creating directory blk/b</c> and exits 23 before any
/// request. On Windows .NET reported the file as <c>ERROR_ALREADY_EXISTS</c>, which mapped to
/// <c>EEXIST</c>; curl ignores that, so Curl sent the request and failed on the write instead.
/// Runs in a fresh temporary directory it removes afterwards.
/// </summary>
[TestClass]
public sealed class Cgr048Tests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "curl-cgr048-" + Guid.NewGuid().ToString("N"));

    [TestInitialize]
    public void CreateRoot() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void TryCreateDirectory_UnderARegularFile_IsNotADirectory()
    {
        string file = Path.Combine(root, "blk");
        File.WriteAllText(file, "x");

        bool created = new PhysicalOutputPaths().TryCreateDirectory(Path.Combine(file, "b"), out int errorNumber);

        Assert.IsFalse(created);
        Assert.AreEqual(CRuntimeErrorNumbers.NotADirectory, errorNumber);
    }

    [TestMethod]
    public void TryCreateDirectory_NameWithANullCharacter_IsInvalidArgument()
    {
        // With a file in the way caught before the create, this is what still reaches the
        // create's failure on every platform: .NET refuses the name with an ArgumentException.
        bool created = new PhysicalOutputPaths().TryCreateDirectory(Path.Combine(root, "a\0b"), out int errorNumber);

        Assert.IsFalse(created);
        Assert.AreEqual(22, errorNumber);
    }

    [TestMethod]
    public void CreateLeadingDirectories_ThroughARegularFile_FailsOnTheDirectoryUnderIt()
    {
        File.WriteAllText(Path.Combine(root, "blk"), "x");
        string outputFile = Path.Combine(root, "blk", "b", "c.txt");

        DirectoryCreationFailure? failure = OutputFileDirectories.CreateLeadingDirectories(
            new PhysicalOutputPaths(), outputFile, OperatingSystem.IsWindows());

        Assert.IsNotNull(failure);
        Assert.AreEqual($"curl: Error creating directory {Path.Combine(root, "blk", "b")}", failure.Message(CRuntimeErrorNumbers.Windows));
    }
}
