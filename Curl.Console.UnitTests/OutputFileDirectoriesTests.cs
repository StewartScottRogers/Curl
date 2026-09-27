namespace Curl.Console;

/// <summary>
/// Pins the directories <c>--create-dirs</c> creates for an output path, measured with curl
/// 8.21.0 on 2026-09-27 (BL-239 Notes).
/// </summary>
[TestClass]
public sealed class OutputFileDirectoriesTests
{
    private readonly InMemoryFileSystem paths = new();

    [TestMethod]
    public void CreateLeadingDirectories_NestedPath_CreatesEachDirectoryInOrder()
    {
        Assert.IsNull(OutputFileDirectories.CreateLeadingDirectories(paths, "a/b/c.txt", runsOnWindows: false));

        CollectionAssert.AreEqual(new[] { "a", "a/b" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_OnWindowsBackslashes_SeparateDirectories()
    {
        Assert.IsNull(OutputFileDirectories.CreateLeadingDirectories(paths, "x\\y\\z.txt", runsOnWindows: true));

        CollectionAssert.AreEqual(new[] { "x", "x\\y" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_NotOnWindowsBackslashes_AreNameCharacters()
    {
        Assert.IsNull(OutputFileDirectories.CreateLeadingDirectories(paths, "x\\y/z.txt", runsOnWindows: false));

        CollectionAssert.AreEqual(new[] { "x\\y" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_AbsoluteAndDoubledSeparators_SkipsTheEmptyParts()
    {
        Assert.IsNull(OutputFileDirectories.CreateLeadingDirectories(paths, "/a//b/c.txt", runsOnWindows: false));

        CollectionAssert.AreEqual(new[] { "/a", "/a//b" }, paths.CreatedDirectories);
    }

    [TestMethod]
    public void CreateLeadingDirectories_FileNameOnly_CreatesNothing()
    {
        Assert.IsNull(OutputFileDirectories.CreateLeadingDirectories(paths, "c.txt", runsOnWindows: true));

        Assert.AreEqual(0, paths.CreatedDirectories.Count);
    }

    [TestMethod]
    public void CreateLeadingDirectories_DirectoryFails_GivesItAndStops()
    {
        paths.UncreatableDirectories.Add("blk/b");

        Assert.AreEqual("blk/b", OutputFileDirectories.CreateLeadingDirectories(paths, "blk/b/c/d.txt", runsOnWindows: false));

        CollectionAssert.AreEqual(new[] { "blk" }, paths.CreatedDirectories);
    }
}
