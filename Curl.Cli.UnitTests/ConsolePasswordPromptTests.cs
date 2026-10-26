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

    /// <summary>
    /// curl 8.22.0 Schannel's <c>getpass_r</c> writes <c>fputs("\n", tool_stderr)</c> after the
    /// password on a text-mode stderr, so the bytes are CR LF (measured: its stderr turns every
    /// <c>\n</c> into <c>0D 0A</c>).
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ReadPassword_OnWindows_WritesThePromptThenCrLfAfterThePassword()
    {
        AssertPromptBytes("Enter host password for user 'bob':\r\n");
    }

    /// <summary>
    /// On a POSIX terminal curl 8.22.0's <c>getpass_r</c> turns echo off and, once the password is
    /// read, writes <c>fputs("\n", tool_stderr)</c>: a single LF.
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ReadPassword_OffWindows_WritesThePromptThenLfAfterThePassword()
    {
        AssertPromptBytes("Enter host password for user 'bob':\n");
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

    /// <summary>
    /// Off Windows curl 8.22.0's <c>getpass_r</c> writes the LF after the password only
    /// <c>if(disabled)</c>: when <c>ttyecho(FALSE, fd)</c> could not turn echo off (no terminal on
    /// <c>/dev/tty</c> or standard input), the prompt is all that reaches stderr.
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ReadPassword_OffWindowsEchoNotTurnedOff_WritesNoLfAfterThePassword()
    {
        AssertPromptBytesWithEcho(echoTurnsOff: false, "Enter host password for user 'bob':");
    }

    /// <summary>Off Windows, when <c>ttyecho(FALSE, fd)</c> turned echo off, curl 8.22.0 writes one LF after the password.</summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ReadPassword_OffWindowsEchoTurnedOff_WritesLfAfterThePassword()
    {
        AssertPromptBytesWithEcho(echoTurnsOff: true, "Enter host password for user 'bob':\n");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ReadPassword_EchoControl_WritesTheLineTerminatorAndTurnsEchoOnOnlyWhenEchoWasTurnedOff(bool echoTurnsOff)
    {
        StringWriter standardError = new();
        int echoTurnedOn = 0;
        ConsolePasswordPrompt prompt = new(standardError, ScriptedKeys("secret\n"), () => echoTurnsOff, () => echoTurnedOn++);
        Diagnostics.Arrange("echo turns off", echoTurnsOff);

        string password = ReadPassword(prompt, "p:", standardError);

        string expected = echoTurnsOff ? "p:" + standardError.NewLine : "p:";
        Diagnostics.Diff("stderr", expected, standardError.ToString());
        Diagnostics.Assert("echo turned on", echoTurnsOff ? 1 : 0, echoTurnedOn);
        Assert.AreEqual("secret", password);
        Assert.AreEqual(expected, standardError.ToString());
        Assert.AreEqual(echoTurnsOff ? 1 : 0, echoTurnedOn);
    }

    [TestMethod]
    public void ForPlatform_WindowsAndOffWindows_AreDifferentPrompts()
    {
        Diagnostics.Arrange("platforms", "Windows, then not Windows");

        ConsolePasswordPrompt windows = ConsolePasswordPrompt.ForPlatform(isWindows: true, TextWriter.Null);
        ConsolePasswordPrompt offWindows = ConsolePasswordPrompt.ForPlatform(isWindows: false, TextWriter.Null);

        Diagnostics.Assert("different prompts", true, !ReferenceEquals(windows, offWindows));
        Assert.AreNotSame(windows, offWindows);
    }

    /// <summary>Reads <c>secret</c> then Enter through a prompt on a default <see cref="StringWriter"/> (whose line terminator is the platform's, as on <see cref="Console.Error"/>) and pins what went to stderr.</summary>
    private void AssertPromptBytes(string expectedStandardError)
    {
        StringWriter standardError = new();
        ConsolePasswordPrompt prompt = new(standardError, ScriptedKeys("secret\r"));

        string password = ReadPassword(prompt, "Enter host password for user 'bob':", standardError);

        Diagnostics.Diff("stderr", expectedStandardError, standardError.ToString());
        Assert.AreEqual("secret", password);
        Assert.AreEqual(expectedStandardError, standardError.ToString());
    }

    /// <summary>Reads <c>secret</c> then LF through a prompt whose echo control reports <paramref name="echoTurnsOff"/> and pins what went to stderr.</summary>
    private void AssertPromptBytesWithEcho(bool echoTurnsOff, string expectedStandardError)
    {
        StringWriter standardError = new();
        ConsolePasswordPrompt prompt = new(standardError, ScriptedKeys("secret\n"), () => echoTurnsOff, () => { });
        Diagnostics.Arrange("echo turns off", echoTurnsOff);

        string password = ReadPassword(prompt, "Enter host password for user 'bob':", standardError);

        Diagnostics.Diff("stderr", expectedStandardError, standardError.ToString());
        Assert.AreEqual("secret", password);
        Assert.AreEqual(expectedStandardError, standardError.ToString());
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
