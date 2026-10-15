using System.Text;

namespace Curl.Api.Options.Behaviour;

[TestClass]
public sealed class EasyBehaviourOptionsTests
{
    private const UnixFileMode Octal644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private const UnixFileMode Octal755 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private static readonly EasyBehaviourOptions Defaults = new();

    [TestMethod]
    public void New_EasyBehaviourOptions_LeavesEverySwitchButNoProgressOff()
    {
        Assert.IsFalse(Defaults.Verbose);
        Assert.IsFalse(Defaults.IncludeHeader);
        Assert.IsTrue(Defaults.NoProgress);
        Assert.IsFalse(Defaults.NoSignal);
        Assert.IsFalse(Defaults.WildcardMatch);
        Assert.IsFalse(Defaults.FailOnError);
        Assert.IsFalse(Defaults.KeepSendingOnError);
        Assert.IsFalse(Defaults.QuickExit);
    }

    [TestMethod]
    public void New_EasyBehaviourOptions_LeavesErrorBufferStderrPrivateAndShareUnset()
    {
        Assert.IsNull(Defaults.ErrorBuffer);
        Assert.IsNull(Defaults.StandardError);
        Assert.IsNull(Defaults.Private);
        Assert.IsNull(Defaults.Share);
    }

    [TestMethod]
    public void New_EasyBehaviourOptions_CreatesFiles0644AndDirectories0755()
    {
        Assert.AreEqual(Convert.ToInt32("644", 8), (int)Defaults.NewFilePermissions);
        Assert.AreEqual(Convert.ToInt32("755", 8), (int)Defaults.NewDirectoryPermissions);
    }

    [TestMethod]
    public void With_EachProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(Defaults with { Verbose = true }, o => o with { Verbose = false });
        AssertChangesOnly(Defaults with { IncludeHeader = true }, o => o with { IncludeHeader = false });
        AssertChangesOnly(Defaults with { NoProgress = false }, o => o with { NoProgress = true });
        AssertChangesOnly(Defaults with { NoSignal = true }, o => o with { NoSignal = false });
        AssertChangesOnly(Defaults with { WildcardMatch = true }, o => o with { WildcardMatch = false });
        AssertChangesOnly(Defaults with { ErrorBuffer = new StringBuilder() }, o => o with { ErrorBuffer = null });
        AssertChangesOnly(Defaults with { StandardError = Stream.Null }, o => o with { StandardError = null });
        AssertChangesOnly(Defaults with { FailOnError = true }, o => o with { FailOnError = false });
        AssertChangesOnly(Defaults with { KeepSendingOnError = true }, o => o with { KeepSendingOnError = false });
        AssertChangesOnly(Defaults with { Private = "mine" }, o => o with { Private = null });
        AssertChangesOnly(Defaults with { Share = new object() }, o => o with { Share = null });
        AssertChangesOnly(Defaults with { QuickExit = true }, o => o with { QuickExit = false });
        AssertChangesOnly(Defaults with { NewFilePermissions = UnixFileMode.UserRead }, o => o with { NewFilePermissions = Octal644 });
        AssertChangesOnly(Defaults with { NewDirectoryPermissions = UnixFileMode.UserExecute }, o => o with { NewDirectoryPermissions = Octal755 });
    }

    private static void AssertChangesOnly(EasyBehaviourOptions changed, Func<EasyBehaviourOptions, EasyBehaviourOptions> restore)
    {
        Assert.AreNotEqual(Defaults, changed);
        Assert.AreEqual(Defaults, restore(changed));
    }
}
