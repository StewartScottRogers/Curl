namespace Curl.Networking;

/// <summary>
/// <see cref="TerminalPemPassPhrasePrompt.ReadConsoleLine" />: the pass phrase is read from a
/// console without echoing a key (AF-0534), and as a line from redirected input.
/// </summary>
[TestClass]
public sealed class TerminalPemPassPhrasePromptTests
{
    [TestMethod]
    public void ReadConsoleLine_FromConsole_InterceptsEveryKeyIncludingTheFirst()
    {
        var keys = new Queue<ConsoleKeyInfo>([Key('a', ConsoleKey.A), Key('b', ConsoleKey.B), Key('\r', ConsoleKey.Enter)]);
        var intercepts = new List<bool>();

        var answer = TerminalPemPassPhrasePrompt.ReadConsoleLine(false, TextReader.Null, intercept =>
        {
            intercepts.Add(intercept);
            return keys.Dequeue();
        });

        Assert.AreEqual("ab", answer);
        CollectionAssert.AreEqual(new[] { true, true, true }, intercepts);
    }

    [TestMethod]
    public void ReadConsoleLine_FromRedirectedInput_ReadsOneLineAndNoKey()
    {
        var answer = TerminalPemPassPhrasePrompt.ReadConsoleLine(true, new StringReader("secret\nnext\n"), _ => throw new AssertFailedException("no key is read"));

        Assert.AreEqual("secret", answer);
    }

    private static ConsoleKeyInfo Key(char keyChar, ConsoleKey key) => new(keyChar, key, shift: false, alt: false, control: false);
}
