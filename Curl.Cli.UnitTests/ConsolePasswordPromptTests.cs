namespace Curl.Cli;

/// <summary>
/// Pins <see cref="ConsolePasswordPrompt"/> against scripted keys and a string writer in place of
/// the console: the prompt is written as given, keys are read until Enter, backspace erases, a line
/// terminator follows the password, and an unreadable console ends the password.
/// </summary>
[TestClass]
public sealed class ConsolePasswordPromptTests
{
    [TestMethod]
    [DataRow("secret\r", "secret")]
    [DataRow("secret\n", "secret")]
    [DataRow("\r", "")]
    [DataRow("sx\bec\b\bcret\r", "scret")]
    [DataRow("\b\bab\r", "ab")]
    public void ReadPassword_KeysUntilEnter_ReturnsTheTypedPassword(string keys, string expectedPassword)
    {
        StringWriter standardError = new();
        ConsolePasswordPrompt prompt = new(standardError, ScriptedKeys(keys));

        string password = prompt.ReadPassword("Enter host password for user 'bob':");

        Assert.AreEqual(expectedPassword, password);
        Assert.AreEqual("Enter host password for user 'bob':" + standardError.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void ReadPassword_ConsoleCannotBeRead_ReturnsWhatWasTyped()
    {
        StringWriter standardError = new();
        ConsolePasswordPrompt prompt = new(standardError, ScriptedKeys("ab"));

        string password = prompt.ReadPassword("p:");

        Assert.AreEqual("ab", password);
        Assert.AreEqual("p:" + standardError.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void ForProcessConsole_IsOneSharedInstance()
    {
        Assert.AreSame(ConsolePasswordPrompt.ForProcessConsole, ConsolePasswordPrompt.ForProcessConsole);
    }

    [TestMethod]
    public void ForProcessConsole_InputRedirected_ReturnsAnEmptyPassword()
    {
        if (!Console.IsInputRedirected)
        {
            Assert.Inconclusive("Standard input is a console here; reading a key would wait for a keypress.");
        }

        string password = ConsolePasswordPrompt.ForProcessConsole.ReadPassword(string.Empty);

        Assert.AreEqual(string.Empty, password);
    }

    /// <summary>Returns each character of <paramref name="keys"/> as a key, then throws as an unreadable console does.</summary>
    private static Func<ConsoleKeyInfo> ScriptedKeys(string keys)
    {
        Queue<char> remaining = new(keys);
        return () => remaining.TryDequeue(out char key)
            ? new ConsoleKeyInfo(key, default, shift: false, alt: false, control: false)
            : throw new InvalidOperationException("Cannot read keys when input is redirected.");
    }
}
