namespace Curl.Cli;

/// <summary>
/// Pins that the account home directory is read from the user database only off Windows, where curl
/// searches it, so Windows never loads <c>shell32.dll</c> and the <c>user32.dll</c> it brings (BL-1290).
/// </summary>
[TestClass]
public sealed class AccountHomeDirectoryTests
{
    [TestMethod]
    public void ReadOffWindows_OnWindows_IsNullWithoutReadingTheUserDatabase()
    {
        bool read = false;

        string? directory = AccountHomeDirectory.ReadOffWindows(isWindows: true, () =>
        {
            read = true;
            return "/account";
        });

        Assert.IsNull(directory);
        Assert.IsFalse(read);
    }

    [TestMethod]
    public void ReadOffWindows_OffWindows_IsTheDirectoryTheUserDatabaseGives()
    {
        string? directory = AccountHomeDirectory.ReadOffWindows(isWindows: false, () => "/account");

        Assert.AreEqual("/account", directory);
    }

    [TestMethod]
    public void ReadFromUserDatabase_OnAnyPlatform_IsTheUserProfileFolder()
    {
        string directory = AccountHomeDirectory.ReadFromUserDatabase();

        Assert.AreEqual(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), directory);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ForProcess_OnWindows_IsNull()
    {
        Assert.IsNull(AccountHomeDirectory.ForProcess);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ForProcess_OffWindows_IsTheUserProfileFolder()
    {
        Assert.AreEqual(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AccountHomeDirectory.ForProcess);
    }
}
