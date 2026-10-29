namespace Curl.Cli;

/// <summary>
/// CGR-183: with standard input not a terminal, curl 8.22.0's <c>getpass_r</c> cannot turn echo off,
/// so it writes the prompt and no trailing newline (ADR-0775); the gap report measured 8.18.0.
/// </summary>
[TestClass]
public sealed class Cgr183Tests
{
    [TestMethod]
    public void ReadPassword_StandardInputNotATerminal_WritesNoNewlineAfterThePrompt()
    {
        StringWriter standardError = new();
        Queue<char> keys = new("secret\n");
        ConsolePasswordPrompt prompt = new(standardError, () => new ConsoleKeyInfo(keys.Dequeue(), ConsoleKey.NoName, false, false, false), () => false, () => Assert.Fail("echo was not turned off"));

        string password = prompt.ReadPassword("Enter host password for user 'user':");

        Assert.AreEqual("secret", password);
        Assert.AreEqual("Enter host password for user 'user':", standardError.ToString());
    }
}
