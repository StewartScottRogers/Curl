using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_WithoutMailOptions_LeavesCurlsDefaults()
    {
        CommandLineOptions options = Accept();

        Diagnostics.Assert("mail from", null, options.MailFrom);
        Diagnostics.Assert("mail recipients", "[]", CommandLineParseDiagnostics.QuoteEach(options.MailRecipients));
        Diagnostics.Assert("mail auth", null, options.MailAuth);
        Diagnostics.Assert("mail recipient allow fails", false, options.MailRecipientAllowFails);
        Diagnostics.Assert("upload flags", ImapUploadFlags.Seen, options.UploadFlags);
        Diagnostics.Assert("login options", null, options.LoginOptions);
        Diagnostics.Assert("sasl authorization identity", null, options.SaslAuthorizationIdentity);
        Diagnostics.Assert("sasl initial response", false, options.SaslInitialResponse);
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
        string? mailFrom = Accept("--mail-from", "a@example.com", "--mail-from", "<b@example.com>").MailFrom;

        Diagnostics.Assert("mail from", "<b@example.com>", mailFrom);
        Assert.AreEqual("<b@example.com>", mailFrom);
    }

    [TestMethod]
    public void Parse_MailAuthGivenTwice_KeepsTheLastVerbatim()
    {
        string? mailAuth = Accept("--mail-auth", "a@example.com", "--mail-auth", "b@example.com").MailAuth;

        Diagnostics.Assert("mail auth", "b@example.com", mailAuth);
        Assert.AreEqual("b@example.com", mailAuth);
    }

    [TestMethod]
    public void Parse_SaslAuthzidGivenTwice_KeepsTheLast()
    {
        string? authorizationIdentity = Accept("--sasl-authzid", "first", "--sasl-authzid", "second").SaslAuthorizationIdentity;

        Diagnostics.Assert("sasl authorization identity", "second", authorizationIdentity);
        Assert.AreEqual("second", authorizationIdentity);
    }

    [TestMethod]
    public void Parse_LoginOptionsGivenTwice_KeepsTheLastVerbatim()
    {
        string? loginOptions = Accept("--login-options", "AUTH=*", "--login-options", "AUTH=PLAIN").LoginOptions;

        Diagnostics.Assert("login options", "AUTH=PLAIN", loginOptions);
        Assert.AreEqual("AUTH=PLAIN", loginOptions);
    }

    [TestMethod]
    public void Parse_EmptyLoginOptions_IsAcceptedAndKept()
    {
        string? loginOptions = Accept("--login-options", "").LoginOptions;

        Diagnostics.Assert("login options", "\"\"", loginOptions is null ? null : "\"" + loginOptions + "\"");
        Assert.AreEqual(string.Empty, loginOptions);
    }

    [TestMethod]
    public void Parse_RepeatedMailRcpt_KeepsEveryRecipientInCommandLineOrder()
    {
        CommandLineOptions options = Accept("--mail-rcpt", "c@example.com", "--mail-rcpt", "a@example.com", "--mail-rcpt", "b@example.com");

        Diagnostics.Assert(
            "mail recipients",
            CommandLineParseDiagnostics.QuoteEach(["c@example.com", "a@example.com", "b@example.com"]),
            CommandLineParseDiagnostics.QuoteEach(options.MailRecipients));
        CollectionAssert.AreEqual(new[] { "c@example.com", "a@example.com", "b@example.com" }, options.MailRecipients.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyMailRcpt_IsAcceptedAndKept()
    {
        IReadOnlyList<string> recipients = Accept("--mail-rcpt", "").MailRecipients;

        Diagnostics.Assert("mail recipients", CommandLineParseDiagnostics.QuoteEach([string.Empty]), CommandLineParseDiagnostics.QuoteEach(recipients));
        CollectionAssert.AreEqual(new[] { string.Empty }, recipients.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "--mail-rcpt-allowfails" }, true)]
    [DataRow(new[] { "--mail-rcpt-allowfails", "--no-mail-rcpt-allowfails" }, false)]
    [DataRow(new[] { "--no-mail-rcpt-allowfails" }, false)]
    [DataRow(new[] { "--no-mail-rcpt-allowfails", "--mail-rcpt-allowfails" }, true)]
    public void Parse_MailRcptAllowFails_TheLastSpellingWins(string[] arguments, bool expected)
    {
        bool allowFails = Accept(arguments).MailRecipientAllowFails;

        Diagnostics.Assert("mail recipient allow fails", expected, allowFails);
        Assert.AreEqual(expected, allowFails);
    }

    [TestMethod]
    [DataRow(new[] { "--sasl-ir" }, true)]
    [DataRow(new[] { "--sasl-ir", "--no-sasl-ir" }, false)]
    [DataRow(new[] { "--no-sasl-ir" }, false)]
    [DataRow(new[] { "--no-sasl-ir", "--sasl-ir" }, true)]
    public void Parse_SaslIr_TheLastSpellingWins(string[] arguments, bool expected)
    {
        bool initialResponse = Accept(arguments).SaslInitialResponse;

        Diagnostics.Assert("sasl initial response", expected, initialResponse);
        Assert.AreEqual(expected, initialResponse);
    }

    [TestMethod]
    [DataRow("seen,-draft", ImapUploadFlags.Seen)]
    [DataRow("-seen", ImapUploadFlags.None)]
    [DataRow("answered,deleted,draft,flagged", ImapUploadFlags.Answered | ImapUploadFlags.Deleted | ImapUploadFlags.Draft | ImapUploadFlags.Flagged | ImapUploadFlags.Seen)]
    [DataRow("flagged,-flagged", ImapUploadFlags.Seen)]
    [DataRow("-seen,answered", ImapUploadFlags.Answered)]
    public void Parse_UploadFlags_SetsAndClearsFlagsLeftToRightFromSeen(string value, ImapUploadFlags expected)
    {
        ImapUploadFlags uploadFlags = Accept("--upload-flags", value).UploadFlags;

        Diagnostics.Assert("upload flags", expected, uploadFlags);
        Assert.AreEqual(expected, uploadFlags);
    }

    [TestMethod]
    public void Parse_UploadFlagsGivenTwice_AddsToTheFlagsBefore()
    {
        ImapUploadFlags uploadFlags = Accept("--upload-flags", "draft", "--upload-flags", "flagged").UploadFlags;

        Diagnostics.Assert("upload flags", ImapUploadFlags.Draft | ImapUploadFlags.Flagged | ImapUploadFlags.Seen, uploadFlags);
        Assert.AreEqual(ImapUploadFlags.Draft | ImapUploadFlags.Flagged | ImapUploadFlags.Seen, uploadFlags);
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
        AssertRefused(Parse(["--upload-flags", value, Url]), "curl: option --upload-flags: is unknown");
    }

    [TestMethod]
    [DataRow("--mail-from")]
    [DataRow("--mail-auth")]
    [DataRow("--sasl-authzid")]
    public void Parse_EmptyTextMailOption_IsRefusedAsBlank(string option)
    {
        AssertRefused(Parse([option, "", Url]), $"curl: option {option}: blank argument where content is expected");
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
        AssertRefused(Parse([Url, option]), $"curl: option {option}: requires parameter");
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
        AssertRefused(Parse([option, "x", Url]), $"curl: option {option}: the given option cannot be reversed with a --no- prefix");
    }

    private CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach([optionLine, TryHelp]),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
