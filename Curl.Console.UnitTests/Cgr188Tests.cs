using Curl.Engine;

namespace Curl.Console;

/// <summary>
/// CGR-188 (BL-3717): on Windows <c>--create-dirs</c> for an output path past MAX_PATH fails in
/// curl (<c>curl: Error creating directory ...</c>, exit 23, nothing sent) where .NET's long-path
/// support made the directories. Runs in a fresh temporary directory it removes afterwards.
/// </summary>
[TestClass]
public sealed class Cgr188Tests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "curl-cgr188-" + Guid.NewGuid().ToString("N"));

    [TestInitialize]
    public void CreateRoot() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void TryCreateDirectory_PathAtTheLimit_IsNoSuchFileOrDirectoryAndNotMade()
    {
        string path = Path.Combine(root, "d");
        var paths = new PhysicalOutputPaths(null, CRuntimeErrorNumbers.Windows, null, null, path.Length);

        bool created = paths.TryCreateDirectory(path, out int errorNumber);

        Assert.IsFalse(created);
        Assert.AreEqual(CRuntimeErrorNumbers.NoSuchFileOrDirectory, errorNumber);
        Assert.IsFalse(Directory.Exists(path));
    }

    [TestMethod]
    public void TryCreateDirectory_PathUnderTheLimit_IsMade()
    {
        string path = Path.Combine(root, "d");
        var paths = new PhysicalOutputPaths(null, CRuntimeErrorNumbers.Windows, null, null, path.Length + 1);

        bool created = paths.TryCreateDirectory(path, out int errorNumber);

        Assert.IsTrue(created);
        Assert.AreEqual(0, errorNumber);
        Assert.IsTrue(Directory.Exists(path));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryCreateDirectory_ProductionOnWindows_RefusesAPathOf248Characters()
    {
        string path = Path.Combine(root, new string('d', 248 - root.Length - 1));

        bool created = new PhysicalOutputPaths().TryCreateDirectory(path, out int errorNumber);

        Assert.IsFalse(created);
        Assert.AreEqual(CRuntimeErrorNumbers.NoSuchFileOrDirectory, errorNumber);
    }
}
