using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins that the account home directory is read from the user database only off Windows, where curl
/// searches it, so Windows never loads <c>shell32.dll</c> and the <c>user32.dll</c> it brings (BL-1290).
/// </summary>
[TestClass]
public sealed class AccountHomeDirectoryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ReadOffWindows_OnWindows_IsNullWithoutReadingTheUserDatabase()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("is windows", true);
        diagnostics.Arrange("user database gives", "/account");
        bool read = false;

        string? directory = AccountHomeDirectory.ReadOffWindows(isWindows: true, () =>
        {
            read = true;
            return "/account";
        });
        diagnostics.Act("directory", directory);
        diagnostics.Act("user database read", read);

        diagnostics.Assert("directory", null, directory);
        diagnostics.Assert("user database read", false, read);
        Assert.IsNull(directory);
        Assert.IsFalse(read);
    }

    [TestMethod]
    public void ReadOffWindows_OffWindows_IsTheDirectoryTheUserDatabaseGives()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("is windows", false);
        diagnostics.Arrange("user database gives", "/account");

        string? directory = AccountHomeDirectory.ReadOffWindows(isWindows: false, () => "/account");
        diagnostics.Act("directory", directory);

        diagnostics.Assert("directory", "/account", directory);
        Assert.AreEqual("/account", directory);
    }

    [TestMethod]
    public void ReadFromUserDatabase_OnAnyPlatform_IsTheUserProfileFolder()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        diagnostics.Arrange("user profile folder found", expected.Length > 0);

        string directory = AccountHomeDirectory.ReadFromUserDatabase();
        diagnostics.Act("directory found", directory.Length > 0);

        diagnostics.Assert("directory is the user profile folder", true, directory == expected);
        Assert.AreEqual(expected, directory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ForProcess_OnWindows_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "Windows");

        string? directory = AccountHomeDirectory.ForProcess;
        diagnostics.Act("directory", directory);

        diagnostics.Assert("directory", null, directory);
        Assert.IsNull(AccountHomeDirectory.ForProcess);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ForProcess_OffWindows_IsTheUserProfileFolder()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        diagnostics.Arrange("platform", "not Windows");

        string? directory = AccountHomeDirectory.ForProcess;
        diagnostics.Act("directory found", directory is not null);

        diagnostics.Assert("directory is the user profile folder", true, directory == expected);
        Assert.AreEqual(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AccountHomeDirectory.ForProcess);
    }
}
