namespace Curl.Mcp;

[TestClass]
public sealed class McpConfinementRootTests
{
    private string workspace = string.Empty;

    private string root = string.Empty;

    private string outside = string.Empty;

    [TestInitialize]
    public void CreateFolders()
    {
        workspace = Path.Combine(Path.GetTempPath(), "curl-mcp-root-" + Guid.NewGuid().ToString("N"));
        root = Path.Combine(workspace, "root");
        outside = Path.Combine(workspace, "outside");
        Directory.CreateDirectory(Path.Combine(root, "sub", "deeper"));
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(Path.Combine(workspace, "root-evil"));
        File.WriteAllText(Path.Combine(root, "inside.txt"), "x");
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "x");
    }

    [TestCleanup]
    public void DeleteFolders() => Directory.Delete(workspace, recursive: true);

    [TestMethod]
    public void Constructor_NullEmptyRelativeOrMissingRoot_ThrowsNamingTheCause()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpConfinementRoot(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new McpConfinementRoot(""));
        ArgumentException relative = Assert.ThrowsExactly<ArgumentException>(() => new McpConfinementRoot("relative/folder"));
        StringAssert.Contains(relative.Message, "relative");
        ArgumentException missing = Assert.ThrowsExactly<ArgumentException>(() => new McpConfinementRoot(Path.Combine(workspace, "missing")));
        StringAssert.Contains(missing.Message, "not an existing folder");
    }

    [TestMethod]
    public void FromEnvironment_Unset_IsNotConfiguredAndRefusesEveryPath()
    {
        McpConfinementRoot confinement = McpConfinementRoot.FromEnvironment(_ => null);

        Assert.AreSame(McpConfinementRoot.NotConfigured, confinement);
        Assert.IsFalse(confinement.IsConfigured);
        Assert.IsNull(confinement.ResolvedRoot);
        McpPathCheck check = confinement.Check(Path.Combine(root, "inside.txt"));
        Assert.AreEqual(McpPathVerdict.NoRootConfigured, check.Verdict);
        Assert.IsFalse(check.IsAllowed);
        Assert.IsNull(check.ResolvedPath);
        StringAssert.Contains(check.Reason, "CURL_MCP_ROOT is not set");
    }

    [TestMethod]
    public void FromEnvironment_Set_ReadsCurlMcpRoot()
    {
        string? asked = null;

        McpConfinementRoot confinement = McpConfinementRoot.FromEnvironment(name =>
        {
            asked = name;
            return root;
        });

        Assert.AreEqual("CURL_MCP_ROOT", asked);
        Assert.IsTrue(confinement.IsConfigured);
        Assert.IsTrue(confinement.Check("inside.txt").IsAllowed);
    }

    [TestMethod]
    public void FromEnvironment_NullReader_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => McpConfinementRoot.FromEnvironment(null!));

    [TestMethod]
    public void FromEnvironment_SetButEmpty_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => McpConfinementRoot.FromEnvironment(_ => ""));

    [TestMethod]
    public void Check_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpConfinementRoot(root).Check(null!));

    [TestMethod]
    [DataRow("inside.txt")]
    [DataRow("sub/deeper/nested.txt")]
    [DataRow("")]
    [DataRow(".")]
    [DataRow("sub/../inside.txt")]
    [DataRow("./sub/./deeper/../file.txt")]
    [DataRow("sub/deeper\\mixed/separators.txt")]
    [DataRow("sub/not-yet/created/output.bin")]
    public void Check_RelativePathInside_IsAllowed(string path)
    {
        McpPathCheck check = new McpConfinementRoot(root).Check(path);

        Assert.AreEqual(McpPathVerdict.Inside, check.Verdict, check.Reason);
        Assert.IsTrue(check.IsAllowed);
        Assert.AreEqual(path, check.Path);
        StringAssert.Contains(check.Reason, "inside CURL_MCP_ROOT");
    }

    [TestMethod]
    public void Check_AbsolutePathInsideAndTheRootItself_AreAllowed()
    {
        McpConfinementRoot confinement = new(root);

        Assert.IsTrue(confinement.Check(Path.Combine(root, "sub", "deeper", "x.txt")).IsAllowed);
        McpPathCheck itself = confinement.Check(root + Path.DirectorySeparatorChar);
        Assert.IsTrue(itself.IsAllowed);
        Assert.AreEqual(confinement.ResolvedRoot, itself.ResolvedPath);
    }

    [TestMethod]
    [DataRow("../outside/secret.txt")]
    [DataRow("sub/../../outside/secret.txt")]
    [DataRow("..")]
    [DataRow("../root-evil/file.txt")]
    public void Check_RelativePathEscaping_IsOutside(string path)
    {
        McpPathCheck check = new McpConfinementRoot(root).Check(path);

        Assert.AreEqual(McpPathVerdict.Outside, check.Verdict);
        Assert.IsFalse(check.IsAllowed);
        StringAssert.Contains(check.Reason, "outside CURL_MCP_ROOT");
    }

    [TestMethod]
    public void Check_AbsolutePathOutsideAndSiblingSharingTheRootsName_AreOutside()
    {
        McpConfinementRoot confinement = new(root);

        Assert.AreEqual(McpPathVerdict.Outside, confinement.Check(Path.Combine(outside, "secret.txt")).Verdict);
        Assert.AreEqual(McpPathVerdict.Outside, confinement.Check(Path.Combine(workspace, "root-evil")).Verdict);
        Assert.AreEqual(McpPathVerdict.Outside, confinement.Check(root + "-evil" + Path.DirectorySeparatorChar + "x").Verdict);
    }

    [TestMethod]
    public void Check_DotDotPastTheFileSystemRoot_StaysAtTheFileSystemRootAndIsOutside()
    {
        string climb = string.Concat(Enumerable.Repeat("../", 64)) + "x.txt";

        McpPathCheck check = new McpConfinementRoot(root).Check(climb);

        Assert.AreEqual(McpPathVerdict.Outside, check.Verdict);
        Assert.AreEqual(Path.Combine(Path.GetPathRoot(check.ResolvedPath)!, "x.txt"), check.ResolvedPath);
    }

    [TestMethod]
    public void Check_FileSystemRootAsTheRoot_AllowsEverythingOnIt()
    {
        string fileSystemRoot = Path.GetPathRoot(Path.GetFullPath(workspace))!;

        McpConfinementRoot confinement = new(fileSystemRoot);

        Assert.AreEqual(fileSystemRoot, confinement.ResolvedRoot);
        Assert.IsTrue(confinement.Check(Path.Combine(outside, "secret.txt")).IsAllowed);
        Assert.IsTrue(confinement.Check(fileSystemRoot).IsAllowed);
    }

    [TestMethod]
    public void Check_InvalidPath_IsUnresolvable()
    {
        McpPathCheck check = new McpConfinementRoot(root).Check("bad\0name");

        Assert.AreEqual(McpPathVerdict.Unresolvable, check.Verdict);
        Assert.IsFalse(check.IsAllowed);
        Assert.IsNull(check.ResolvedPath);
        StringAssert.Contains(check.Reason, "cannot be resolved");
    }

    [TestMethod]
    public void Check_SymbolicLinkInsidePointingOutside_IsOutside()
    {
        CreateDirectoryLink(Path.Combine(root, "escape"), outside);
        CreateFileLink(Path.Combine(root, "secret-link.txt"), Path.Combine(outside, "secret.txt"));
        McpConfinementRoot confinement = new(root);

        McpPathCheck throughFolder = confinement.Check("escape/secret.txt");
        McpPathCheck throughFile = confinement.Check("secret-link.txt");

        Assert.AreEqual(McpPathVerdict.Outside, throughFolder.Verdict);
        Assert.AreEqual(McpPathVerdict.Outside, throughFile.Verdict);
        StringAssert.Contains(throughFile.ResolvedPath, "outside");
    }

    [TestMethod]
    public void Check_ParentIsSymbolicLinkToOutside_NewFileUnderItIsOutside()
    {
        CreateDirectoryLink(Path.Combine(root, "sub", "escape"), outside);

        McpPathCheck check = new McpConfinementRoot(root).Check(Path.Combine(root, "sub", "escape", "new-output.bin"));

        Assert.AreEqual(McpPathVerdict.Outside, check.Verdict);
    }

    [TestMethod]
    public void Check_DanglingSymbolicLinkToOutside_IsOutside()
    {
        CreateFileLink(Path.Combine(root, "dangling.txt"), Path.Combine(outside, "not-yet.txt"));

        Assert.AreEqual(McpPathVerdict.Outside, new McpConfinementRoot(root).Check("dangling.txt").Verdict);
    }

    [TestMethod]
    public void Check_SymbolicLinkInsidePointingInside_IsAllowed()
    {
        CreateDirectoryLink(Path.Combine(root, "shortcut"), "sub" + Path.DirectorySeparatorChar + "deeper");
        CreateFileLink(Path.Combine(root, "inside-link.txt"), Path.Combine(root, "inside.txt"));
        McpConfinementRoot confinement = new(root);

        McpPathCheck throughFolder = confinement.Check("shortcut/file.txt");
        McpPathCheck throughFile = confinement.Check("inside-link.txt");

        Assert.IsTrue(throughFolder.IsAllowed, throughFolder.Reason);
        Assert.AreEqual(Path.Combine(confinement.ResolvedRoot!, "sub", "deeper", "file.txt"), throughFolder.ResolvedPath);
        Assert.IsTrue(throughFile.IsAllowed, throughFile.Reason);
    }

    [TestMethod]
    public void Check_DotDotAfterALink_MustStayInsideBothAsWrittenAndStepByStep()
    {
        CreateDirectoryLink(Path.Combine(root, "deep"), Path.Combine(root, "sub", "deeper"));
        CreateDirectoryLink(Path.Combine(root, "up"), workspace);
        McpConfinementRoot confinement = new(root);

        McpPathCheck insideStepByStepOutsideAsWritten = confinement.Check("deep/../../x.txt");
        McpPathCheck insideAsWrittenOutsideStepByStep = confinement.Check("up/../inside.txt");

        Assert.AreEqual(McpPathVerdict.Outside, insideStepByStepOutsideAsWritten.Verdict);
        Assert.AreEqual(McpPathVerdict.Outside, insideAsWrittenOutsideStepByStep.Verdict);
    }

    [TestMethod]
    public void Check_RootReachedThroughASymbolicLink_JudgesTheResolvedRoot()
    {
        string linkedRoot = Path.Combine(workspace, "linked-root");
        CreateDirectoryLink(linkedRoot, root);

        McpConfinementRoot confinement = new(linkedRoot);

        Assert.AreEqual(new McpConfinementRoot(root).ResolvedRoot, confinement.ResolvedRoot);
        Assert.IsTrue(confinement.Check(Path.Combine(linkedRoot, "inside.txt")).IsAllowed);
        Assert.IsTrue(confinement.Check(Path.Combine(root, "inside.txt")).IsAllowed);
    }

    [TestMethod]
    public void Check_SymbolicLinkLoop_IsUnresolvable()
    {
        CreateFileLink(Path.Combine(root, "loop-a"), "loop-b");
        CreateFileLink(Path.Combine(root, "loop-b"), "loop-a");

        McpPathCheck check = new McpConfinementRoot(root).Check("loop-a");

        Assert.AreEqual(McpPathVerdict.Unresolvable, check.Verdict);
        StringAssert.Contains(check.Reason, "symbolic links deep");
    }

    [TestMethod]
    public void ComparisonFor_SwappedCaseFolderExists_IgnoresCase()
    {
        List<string> probed = [];

        StringComparison comparison = McpConfinementRoot.ComparisonFor("/Data/ab", path =>
        {
            probed.Add(path);
            return true;
        });

        Assert.AreEqual(StringComparison.OrdinalIgnoreCase, comparison);
        CollectionAssert.AreEqual(new[] { "/dATA/AB" }, probed);
    }

    [TestMethod]
    public void ComparisonFor_SwappedCaseFolderMissing_ComparesExactly() =>
        Assert.AreEqual(StringComparison.Ordinal, McpConfinementRoot.ComparisonFor("/Data", _ => false));

    [TestMethod]
    public void ComparisonFor_RootWithNoLetters_ComparesExactlyWithoutProbing() =>
        Assert.AreEqual(StringComparison.Ordinal, McpConfinementRoot.ComparisonFor("/123", _ => throw new AssertFailedException("probed")));

    [TestMethod]
    public void Check_CaseRuleMeasuredOnThisFileSystem_DecidesAPathDifferingOnlyInCase()
    {
        McpConfinementRoot confinement = new(root);
        string otherCase = Path.Combine(Path.GetDirectoryName(confinement.ResolvedRoot)!, "ROOT", "inside.txt");
        bool caseInsensitive = Directory.Exists(Path.Combine(workspace, "ROOT"));

        Assert.AreEqual(caseInsensitive, confinement.Check(otherCase).IsAllowed);
    }

    [TestMethod]
    public void IsResolutionFailure_PathAndFileSystemErrorsOnly()
    {
        Assert.IsTrue(McpConfinementRoot.IsResolutionFailure(new ArgumentException()));
        Assert.IsTrue(McpConfinementRoot.IsResolutionFailure(new IOException()));
        Assert.IsTrue(McpConfinementRoot.IsResolutionFailure(new UnauthorizedAccessException()));
        Assert.IsFalse(McpConfinementRoot.IsResolutionFailure(new InvalidOperationException()));
    }

    private static void CreateDirectoryLink(string link, string target) =>
        CreateLinkOrInconclusive(() => Directory.CreateSymbolicLink(link, target));

    private static void CreateFileLink(string link, string target) =>
        CreateLinkOrInconclusive(() => File.CreateSymbolicLink(link, target));

    private static void CreateLinkOrInconclusive(Action create)
    {
        try
        {
            create();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Inconclusive($"This operating system refused to create a symbolic link: {exception.Message}");
        }
    }
}
