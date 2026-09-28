using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins the mail and SASL options: <c>--mail-from</c>, <c>--mail-rcpt</c>, <c>--mail-auth</c>,
/// <c>--mail-rcpt-allowfails</c>, <c>--upload-flags</c>, <c>--login-options</c>,
/// <c>--sasl-authzid</c> and <c>--sasl-ir</c>. Every refusal and every accepted blank value was
/// measured with curl 8.21.0 (Schannel) and <c>Record-CurlExchange.ps1</c> against a loopback URL on
/// 2026-09-28, and the upload flags' effect with <c>Record-CurlExchange.ps1 -Imap</c>, reading the
/// <c>APPEND</c> line (BL-535 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineMailOptionTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    public void Parse_WithoutMailOptions_LeavesCurlsDefaults()
    {
        CommandLineOptions options = Accept();

        Assert.IsNull(options.MailFrom);
        Assert.IsEmpty(options.MailRecipients);
        Assert.IsNull(options.MailAuth);
        Assert.IsFalse(options.MailRecipientAllowFails);
        Assert.AreEqual(ImapUploadFlags.Seen, options.UploadFlags);
        Assert.IsNull(options.LoginOptions);
        Assert.IsNull(options.SaslAuthorizationIdentity);
        Assert.IsFalse(options.SaslInitialResponse);
    }

    [TestMethod]
    public void Parse_MailFromGivenTwice_KeepsTheLastVerbatim()
    {
        Assert.AreEqual("<b@example.com>", Accept("--mail-from", "a@example.com", "--mail-from", "<b@example.com>").MailFrom);
    }

    [TestMethod]
    public void Parse_MailAuthGivenTwice_KeepsTheLastVerbatim()
    {
        Assert.AreEqual("b@example.com", Accept("--mail-auth", "a@example.com", "--mail-auth", "b@example.com").MailAuth);
    }

    [TestMethod]
    public void Parse_SaslAuthzidGivenTwice_KeepsTheLast()
    {
        Assert.AreEqual("second", Accept("--sasl-authzid", "first", "--sasl-authzid", "second").SaslAuthorizationIdentity);
    }

    [TestMethod]
    public void Parse_LoginOptionsGivenTwice_KeepsTheLastVerbatim()
    {
        Assert.AreEqual("AUTH=PLAIN", Accept("--login-options", "AUTH=*", "--login-options", "AUTH=PLAIN").LoginOptions);
    }

    [TestMethod]
    public void Parse_EmptyLoginOptions_IsAcceptedAndKept()
    {
        Assert.AreEqual(string.Empty, Accept("--login-options", "").LoginOptions);
    }

    [TestMethod]
    public void Parse_RepeatedMailRcpt_KeepsEveryRecipientInCommandLineOrder()
    {
        CommandLineOptions options = Accept("--mail-rcpt", "c@example.com", "--mail-rcpt", "a@example.com", "--mail-rcpt", "b@example.com");

        CollectionAssert.AreEqual(new[] { "c@example.com", "a@example.com", "b@example.com" }, options.MailRecipients.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyMailRcpt_IsAcceptedAndKept()
    {
        CollectionAssert.AreEqual(new[] { string.Empty }, Accept("--mail-rcpt", "").MailRecipients.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "--mail-rcpt-allowfails" }, true)]
    [DataRow(new[] { "--mail-rcpt-allowfails", "--no-mail-rcpt-allowfails" }, false)]
    [DataRow(new[] { "--no-mail-rcpt-allowfails" }, false)]
    [DataRow(new[] { "--no-mail-rcpt-allowfails", "--mail-rcpt-allowfails" }, true)]
    public void Parse_MailRcptAllowFails_TheLastSpellingWins(string[] arguments, bool expected)
    {
        Assert.AreEqual(expected, Accept(arguments).MailRecipientAllowFails);
    }

    [TestMethod]
    [DataRow(new[] { "--sasl-ir" }, true)]
    [DataRow(new[] { "--sasl-ir", "--no-sasl-ir" }, false)]
    [DataRow(new[] { "--no-sasl-ir" }, false)]
    [DataRow(new[] { "--no-sasl-ir", "--sasl-ir" }, true)]
    public void Parse_SaslIr_TheLastSpellingWins(string[] arguments, bool expected)
    {
        Assert.AreEqual(expected, Accept(arguments).SaslInitialResponse);
    }

    [TestMethod]
    [DataRow("seen,-draft", ImapUploadFlags.Seen)]
    [DataRow("-seen", ImapUploadFlags.None)]
    [DataRow("answered,deleted,draft,flagged", ImapUploadFlags.Answered | ImapUploadFlags.Deleted | ImapUploadFlags.Draft | ImapUploadFlags.Flagged | ImapUploadFlags.Seen)]
    [DataRow("flagged,-flagged", ImapUploadFlags.Seen)]
    [DataRow("-seen,answered", ImapUploadFlags.Answered)]
    public void Parse_UploadFlags_SetsAndClearsFlagsLeftToRightFromSeen(string value, ImapUploadFlags expected)
    {
        Assert.AreEqual(expected, Accept("--upload-flags", value).UploadFlags);
    }

    [TestMethod]
    public void Parse_UploadFlagsGivenTwice_AddsToTheFlagsBefore()
    {
        Assert.AreEqual(ImapUploadFlags.Draft | ImapUploadFlags.Flagged | ImapUploadFlags.Seen, Accept("--upload-flags", "draft", "--upload-flags", "flagged").UploadFlags);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("")]
    [DataRow("Seen")]
    [DataRow("seen,,draft")]
    [DataRow("-bogus")]
    [DataRow("seen bogus")]
    [DataRow("seen,")]
    [DataRow("-")]
    public void Parse_UploadFlagsWithAnUnknownItem_IsRefusedAsUnknown(string value)
    {
        AssertRefused(CommandLineParser.Parse(["--upload-flags", value, Url]), "curl: option --upload-flags: is unknown");
    }

    [TestMethod]
    [DataRow("--mail-from")]
    [DataRow("--mail-auth")]
    [DataRow("--sasl-authzid")]
    public void Parse_EmptyTextMailOption_IsRefusedAsBlank(string option)
    {
        AssertRefused(CommandLineParser.Parse([option, "", Url]), $"curl: option {option}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--mail-from")]
    [DataRow("--mail-rcpt")]
    [DataRow("--mail-auth")]
    [DataRow("--upload-flags")]
    [DataRow("--login-options")]
    [DataRow("--sasl-authzid")]
    public void Parse_ValueMailOptionLast_IsRefusedAsNeedingParameter(string option)
    {
        AssertRefused(CommandLineParser.Parse([Url, option]), $"curl: option {option}: requires parameter");
    }

    [TestMethod]
    [DataRow("--no-mail-from")]
    [DataRow("--no-mail-rcpt")]
    [DataRow("--no-mail-auth")]
    [DataRow("--no-upload-flags")]
    [DataRow("--no-login-options")]
    [DataRow("--no-sasl-authzid")]
    public void Parse_NegatedValueMailOption_IsRefusedAsNotReversible(string option)
    {
        AssertRefused(CommandLineParser.Parse([option, "x", Url]), $"curl: option {option}: the given option cannot be reversed with a --no- prefix");
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
