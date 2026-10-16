using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins on Linux and macOS, where the CI fast run proves it, that the runtime's <c>Passwd</c> record is
/// read right: the effective user's home directory is an existing absolute directory, and it is the
/// account home directory curl searches for <c>.ssh/known_hosts</c> and <c>.curlrc</c> (BL-2336). That it
/// ignores <c>HOME</c> is pinned against the shell by <c>UserDatabaseHomeDirectoryIntegrationTests</c>.
/// </summary>
[TestClass]
public sealed class UserDatabaseHomeDirectoryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ReadForEffectiveUser_OffWindows_IsAnExistingAbsoluteDirectory()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "not Windows");

        string? directory = UserDatabaseHomeDirectory.ReadForEffectiveUser();
        diagnostics.Act("directory", directory);

        diagnostics.Assert("rooted existing directory", true, directory is not null && Path.IsPathRooted(directory) && Directory.Exists(directory));
        Assert.IsNotNull(directory);
        Assert.IsTrue(Path.IsPathRooted(directory), directory);
        Assert.IsTrue(Directory.Exists(directory), directory);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ForProcess_OffWindows_IsTheUserDatabaseHome()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string? expected = UserDatabaseHomeDirectory.ReadForEffectiveUser();
        diagnostics.Arrange("user database home", expected);

        string? directory = AccountHomeDirectory.ForProcess;
        diagnostics.Act("directory", directory);

        diagnostics.Assert("directory", expected, directory);
        Assert.AreEqual(expected, directory);
    }
}
