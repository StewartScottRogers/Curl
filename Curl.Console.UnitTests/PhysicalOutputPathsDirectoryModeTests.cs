using System.Runtime.Versioning;

namespace Curl.Console;

/// <summary>
/// BL-5097: <c>--create-dirs</c> makes every directory with mode 0750 (less the umask) off Windows,
/// as curl's <c>mkdir</c> does. Each test works in a fresh temporary directory it removes afterwards.
/// </summary>
[TestClass]
public sealed class PhysicalOutputPathsDirectoryModeTests
{
    private const UnixFileMode Allowed = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;

    private readonly string root = Path.Combine(Path.GetTempPath(), "curl-bl5097-" + Guid.NewGuid().ToString("N"));

    [TestInitialize]
    public void CreateRoot() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void UnixDirectoryMode_IsOctal0750()
    {
        string octal = Convert.ToString((int)PhysicalOutputPaths.UnixDirectoryMode, 8);

        Assert.AreEqual("750", octal);
    }

    [TestMethod]
    public void TryCreateDirectory_NestedDirectories_AreAllCreated()
    {
        string leaf = Path.Combine(root, "a", "b", "c");

        bool created = new PhysicalOutputPaths().TryCreateDirectory(leaf, out int errorNumber);

        Assert.IsTrue(created);
        Assert.AreEqual(0, errorNumber);
        Assert.IsTrue(Directory.Exists(leaf));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void TryCreateDirectory_NestedDirectories_GetNoPermissionBeyondMode0750()
    {
        string leaf = Path.Combine(root, "a", "b", "c");

        Assert.IsTrue(new PhysicalOutputPaths().TryCreateDirectory(leaf, out _));

        foreach (string directory in new[] { Path.Combine(root, "a"), Path.Combine(root, "a", "b"), leaf })
        {
            AssertWithinMode0750(directory);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [UnsupportedOSPlatform("windows")]
    public void TryCreateDirectory_RawByteName_GetsNoPermissionBeyondMode0750()
    {
        string made = Path.Combine(root, "r\uDC80x");

        bool created = new PhysicalOutputPaths().TryCreateDirectory(made, out int errorNumber);

        Assert.IsTrue(created);
        Assert.AreEqual(0, errorNumber);
        // .NET cannot name the raw-byte directory again, so the shell's glob finds it and stat reads its mode.
        var start = new System.Diagnostics.ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("stat -c %a \"$1\"/*");
        start.ArgumentList.Add("sh");
        start.ArgumentList.Add(root);
        using var process = System.Diagnostics.Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        int mode = Convert.ToInt32(output, 8);
        Assert.AreEqual(0, mode & ~Convert.ToInt32("750", 8), output);
    }
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void TryCreateDirectory_ExistingDirectory_KeepsItsMode()
    {
        string existing = Path.Combine(root, "existing");
        Directory.CreateDirectory(existing);
        File.SetUnixFileMode(existing, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        Assert.IsTrue(new PhysicalOutputPaths().TryCreateDirectory(existing, out _));

        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
            File.GetUnixFileMode(existing));
    }

    [UnsupportedOSPlatform("windows")]
    private static void AssertWithinMode0750(string directory)
    {
        UnixFileMode mode = File.GetUnixFileMode(directory);

        Assert.AreEqual(UnixFileMode.None, mode & ~Allowed, directory + " is " + mode);
        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            mode & (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));
    }
}
