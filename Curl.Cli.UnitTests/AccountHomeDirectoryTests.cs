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
    public void ReadOffWindows_OffWindowsWithNoUserDatabaseEntry_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("is windows", false);
        diagnostics.Arrange("user database gives", null);

        string? directory = AccountHomeDirectory.ReadOffWindows(isWindows: false, () => null);
        diagnostics.Act("directory", directory);

        diagnostics.Assert("directory", null, directory);
        Assert.IsNull(directory);
    }
}
