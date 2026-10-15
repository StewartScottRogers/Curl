using System.Diagnostics;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the user database read off Windows against the real <c>getpwuid_r</c>: the directory is the
/// one the shell's <c>~user</c> expansion finds in the user database, whatever <c>HOME</c> says (BL-2336).
/// </summary>
[TestClass]
public sealed class UserDatabaseHomeDirectoryIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ReadForEffectiveUser_OffWindows_IsTheUserDatabaseHomeNotHome()
    {
        string expected = await ReadUserDatabaseHomeThroughTheShellAsync();
        Diagnostics.Arrange("shell's ~user", expected);
        Diagnostics.Arrange("HOME", Environment.GetEnvironmentVariable("HOME"));

        string? directory = UserDatabaseHomeDirectory.ReadForEffectiveUser();
        Diagnostics.Act("directory", directory);

        Diagnostics.Assert("directory", expected, directory);
        Assert.AreEqual(expected, directory);
    }

    private async Task<string> ReadUserDatabaseHomeThroughTheShellAsync()
    {
        // ~name expands through getpwnam, never through HOME, which the child is given as a decoy.
        ProcessStartInfo start = new("/bin/sh", ["-c", "eval echo ~$(id -un)"])
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.Environment["HOME"] = "/nonexistent-home-decoy";
        using Process shell = Process.Start(start)!;
        string output = await shell.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        await shell.WaitForExitAsync(TestContext.CancellationToken);
        return output.TrimEnd('\n');
    }
}
