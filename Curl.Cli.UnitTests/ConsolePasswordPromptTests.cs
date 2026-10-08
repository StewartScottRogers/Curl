using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="ConsolePasswordPrompt"/> against scripted keys and a string writer in place of
/// the console: the prompt is written as given, keys are read until Enter, backspace erases, a line
/// terminator follows the password, and an unreadable console ends the password.
/// </summary>
[TestClass]
public sealed class ConsolePasswordPromptTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Bytes("keys", System.Text.Encoding.ASCII.GetBytes(keys));

        string password = ReadPassword(prompt, "Enter host password for user 'bob':", standardError);

        Diagnostics.Assert("password", expectedPassword, password);
        Diagnostics.Diff("stderr", "Enter host password for user 'bob':" + standardError.NewLine, standardError.ToString());
        Assert.AreEqual(expectedPassword, password);
        Assert.AreEqual("Enter host password for user 'bob':" + standardError.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void ReadPassword_ConsoleCannotBeRead_ReturnsWhatWasTyped()
    {
        StringWriter standardError = new();
        ConsolePasswordPrompt prompt = new(standardError, ScriptedKeys("ab"));
        Diagnostics.Arrange("keys, then an unreadable console", "\"ab\"");

        string password = ReadPassword(prompt, "p:", standardError);

        Diagnostics.Assert("password", "ab", password);
        Diagnostics.Diff("stderr", "p:" + standardError.NewLine, standardError.ToString());
        Assert.AreEqual("ab", password);
        Assert.AreEqual("p:" + standardError.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void ForProcessConsole_IsOneSharedInstance()
    {
        Diagnostics.Arrange("property", nameof(ConsolePasswordPrompt.ForProcessConsole));
        var first = ConsolePasswordPrompt.ForProcessConsole;
        var second = ConsolePasswordPrompt.ForProcessConsole;
        Diagnostics.Act("same instance", ReferenceEquals(first, second));
        Diagnostics.Assert("same instance", true, ReferenceEquals(first, second));
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void ForProcessConsole_InputRedirected_ReturnsAnEmptyPassword()
    {
        Diagnostics.Arrange("prompt", "\"\"");
        Diagnostics.Act("input redirected", Console.IsInputRedirected);
        Diagnostics.Assert("input redirected", true, Console.IsInputRedirected);
        if (!Console.IsInputRedirected)
        {
            Assert.Inconclusive("Standard input is a console here; reading a key would wait for a keypress.");
        }

        string password = ConsolePasswordPrompt.ForProcessConsole.ReadPassword(string.Empty);
        Diagnostics.Act("password", "\"" + password + "\"");

        Diagnostics.Assert("password", string.Empty, password);
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

    /// <summary>Reads a password through <paramref name="prompt"/>, writing the prompt, the password and what went to stderr as diagnostics.</summary>
    private string ReadPassword(ConsolePasswordPrompt prompt, string promptText, StringWriter standardError)
    {
        Diagnostics.Arrange("prompt", "\"" + promptText + "\"");
        string password = prompt.ReadPassword(promptText);
        Diagnostics.Act("password", "\"" + password + "\"");
        Diagnostics.Bytes("stderr", System.Text.Encoding.UTF8.GetBytes(standardError.ToString()));
        return password;
    }
}
