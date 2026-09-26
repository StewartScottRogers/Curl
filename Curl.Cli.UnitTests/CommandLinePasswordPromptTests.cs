using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins when the parser asks its <see cref="IPasswordPrompt"/> for a <c>-u</c> / <c>--user</c> password.
/// Measured with the local curl 8.21.0 on 2026-09-26 (<c>timeout 3 curl &lt;arguments&gt; &lt;/dev/null</c>,
/// reading standard error): <c>-u bob bogus://x</c> writes <c>Enter host password for user 'bob':</c>
/// and waits; <c>-u bob:</c> and <c>-u ;opt</c> never prompt; <c>-u bob;opt</c> prompts for <c>'bob'</c>;
/// <c>-u bob -u alice</c> prompts once, for <c>'alice'</c>; <c>-u bob</c> with no URL prompts;
/// <c>-u bob --bogus</c> is refused without a prompt.
/// </summary>
[TestClass]
public sealed class CommandLinePasswordPromptTests
{
    [TestMethod]
    public void Parse_UserWithoutColon_RecordsThePromptedPassword()
    {
        RecordingPasswordPrompt prompt = new("secret");

        CommandLineParseResult result = Parse(["-u", "bob", "http://example.com/"], prompt);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("bob", result.Options.Credentials!.UserName);
        Assert.AreEqual("secret", result.Options.Credentials.Password);
        CollectionAssert.AreEqual(new[] { "Enter host password for user 'bob':" }, prompt.Prompts);
    }

    [TestMethod]
    public void Parse_EmptyUser_PromptsForTheEmptyUser()
    {
        RecordingPasswordPrompt prompt = new("secret");

        CommandLineParseResult result = Parse(["--user", "", "http://example.com/"], prompt);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.Credentials!.UserName);
        Assert.AreEqual("secret", result.Options.Credentials.Password);
        CollectionAssert.AreEqual(new[] { "Enter host password for user '':" }, prompt.Prompts);
    }

    [TestMethod]
    [DataRow("bob:secret", "bob", "secret")]
    [DataRow("bob:se:cret", "bob", "se:cret")]
    [DataRow("bob:", "bob", "")]
    [DataRow(";opt", ";opt", "")]
    public void Parse_UserThatCurlDoesNotPromptFor_NeverPrompts(string value, string expectedUser, string expectedPassword)
    {
        RecordingPasswordPrompt prompt = new("unused");

        CommandLineParseResult result = Parse(["-u", value, "http://example.com/"], prompt);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedUser, result.Options.Credentials!.UserName);
        Assert.AreEqual(expectedPassword, result.Options.Credentials.Password);
        Assert.IsEmpty(prompt.Prompts);
    }

    [TestMethod]
    public void Parse_UserWithLoginOptions_PromptsForTheUserBeforeTheSemicolon()
    {
        RecordingPasswordPrompt prompt = new("secret");

        CommandLineParseResult result = Parse(["-u", "bob;opt", "http://example.com/"], prompt);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("bob;opt", result.Options.Credentials!.UserName);
        Assert.AreEqual("secret", result.Options.Credentials.Password);
        CollectionAssert.AreEqual(new[] { "Enter host password for user 'bob':" }, prompt.Prompts);
    }

    [TestMethod]
    public void Parse_UserGivenTwice_PromptsOnceForTheLast()
    {
        RecordingPasswordPrompt prompt = new("secret");

        CommandLineParseResult result = Parse(["-u", "bob", "-u", "alice", "http://example.com/"], prompt);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("alice", result.Options.Credentials!.UserName);
        CollectionAssert.AreEqual(new[] { "Enter host password for user 'alice':" }, prompt.Prompts);
    }

    [TestMethod]
    public void Parse_UserWithoutColonThenUserWithColon_NeverPrompts()
    {
        RecordingPasswordPrompt prompt = new("unused");

        CommandLineParseResult result = Parse(["-u", "bob", "-u", "alice:pw", "http://example.com/"], prompt);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("pw", result.Options.Credentials!.Password);
        Assert.IsEmpty(prompt.Prompts);
    }

    [TestMethod]
    public void Parse_UserWithoutColonAndNoUrl_PromptsThenRefusesForNoUrl()
    {
        RecordingPasswordPrompt prompt = new("secret");

        CommandLineParseResult result = Parse(["-u", "bob"], prompt);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { "Enter host password for user 'bob':" }, prompt.Prompts);
    }

    [TestMethod]
    public void Parse_UserWithoutColonThenRefusedOption_NeverPrompts()
    {
        RecordingPasswordPrompt prompt = new("unused");

        CommandLineParseResult result = Parse(["-u", "bob", "--bogus"], prompt);

        Assert.IsFalse(result.IsAccepted);
        Assert.IsEmpty(prompt.Prompts);
    }

    [TestMethod]
    public void Parse_NoUserOption_NeverPrompts()
    {
        RecordingPasswordPrompt prompt = new("unused");

        CommandLineParseResult result = Parse(["-s", "-d", "x", "http://example.com/"], prompt);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.Credentials);
        Assert.IsEmpty(prompt.Prompts);
    }

    [TestMethod]
    public void Parse_EmptyCommandLine_NeverPrompts()
    {
        RecordingPasswordPrompt prompt = new("unused");

        CommandLineParseResult result = Parse([], prompt);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(prompt.Prompts);
    }

    [TestMethod]
    public void Parse_NullPasswordPrompt_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineParser.Parse(["http://example.com/"], _ => true, null!));
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, IPasswordPrompt prompt) =>
        CommandLineParser.Parse(arguments, _ => true, prompt);

    private sealed class RecordingPasswordPrompt(string answer) : IPasswordPrompt
    {
        public List<string> Prompts { get; } = [];

        public string ReadPassword(string prompt)
        {
            Prompts.Add(prompt);
            return answer;
        }
    }
}
