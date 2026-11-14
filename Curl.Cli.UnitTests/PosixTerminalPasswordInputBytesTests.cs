namespace Curl.Cli;

/// <summary>
/// Pins the bytes the password prompt writes on a POSIX terminal (BL-5109): the prompt text before the
/// password, the line terminator after it and nothing else (no keypad-transmit escape), with echo
/// restored after a normal and a cancelled read.
/// </summary>
[TestClass]
public sealed class PosixTerminalPasswordInputBytesTests
{
    private const string Prompt = "Enter host password for user 'alice':";

    [TestMethod]
    public void ReadPassword_Terminal_WritesOnlyThePromptThenTheLineTerminator()
    {
        StringWriter standardError = new();
        List<string> events = [];
        ConsolePasswordPrompt prompt = new(standardError, Keys("pw\r"), () => { events.Add("off"); return true; }, () => events.Add("on"));

        string password = prompt.ReadPassword(Prompt);

        Assert.AreEqual("pw", password);
        Assert.AreEqual(Prompt + standardError.NewLine, standardError.ToString());
        Assert.IsFalse(standardError.ToString().Contains('\u001b'));
        CollectionAssert.AreEqual(new[] { "off", "on" }, events);
    }

    [TestMethod]
    public void ReadPassword_InputEndsBeforeEnter_StillRestoresEcho()
    {
        StringWriter standardError = new();
        List<string> events = [];
        ConsolePasswordPrompt prompt = new(standardError, Keys("pw"), () => { events.Add("off"); return true; }, () => events.Add("on"));

        string password = prompt.ReadPassword(Prompt);

        Assert.AreEqual("pw", password);
        CollectionAssert.AreEqual(new[] { "off", "on" }, events);
    }

    private static Func<ConsoleKeyInfo> Keys(string keys)
    {
        int next = 0;
        return () => next < keys.Length
            ? new ConsoleKeyInfo(keys[next++], default, shift: false, alt: false, control: false)
            : throw new InvalidOperationException("ended");
    }
}
