using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-C</c>/<c>--continue-at</c> with a byte offset beside a <c>-d</c> body or a <c>-F</c> form, measured
/// with the local curl 8.21.0 on 2026-10-02 against <c>http://127.0.0.1:1/</c>: refused at transfer setup with
/// <c>curl: cannot mix --continue-at with --data</c> (or <c>--form</c>) and <c>curl: (2) Failed initialization</c>,
/// exit 2, nothing sent (BL-1276, audit finding AF-0020).
/// </summary>
[TestClass]
public sealed class CommandLineContinueAtWithBodyTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string FailedInitialization = "curl: (2) Failed initialization";

    public TestContext TestContext { get; set; } = null!;

    // curl -C 10 -d x URL, -d x -C 10 URL, --data-binary, --data-urlencode, --json, -sS: names --data.
    [TestMethod]
    [DataRow(new[] { "-C", "10", "-d", "x", Url })]
    [DataRow(new[] { "-d", "x", "-C", "10", Url })]
    [DataRow(new[] { "-sS", "-C", "10", "-d", "x", Url })]
    [DataRow(new[] { "--continue-at", "10", "--data-binary", "x", Url })]
    [DataRow(new[] { "-C", "10", "--data-urlencode", "x", Url })]
    [DataRow(new[] { "-C", "10", "--json", "{}", Url })]
    [DataRow(new[] { "-C", "10", "-d", "x", "file:///dir/x" })]
    [DataRow(new[] { "-C", "10", "-d", "x", Url, Url + "x" })]
    public void Parse_OffsetWithData_IsRefusedNamingData(string[] arguments)
    {
        AssertRefusedAtTransferSetup(
            Parse(arguments),
            "curl: cannot mix --continue-at with --data",
            FailedInitialization);
    }

    // curl -C 10 -F a=b URL and -F a=b -C 10 URL: names --form.
    [TestMethod]
    [DataRow(new[] { "-C", "10", "-F", "a=b", Url })]
    [DataRow(new[] { "--form-string", "a=b", "-C", "10", Url })]
    public void Parse_OffsetWithForm_IsRefusedNamingForm(string[] arguments)
    {
        AssertRefusedAtTransferSetup(
            Parse(arguments),
            "curl: cannot mix --continue-at with --form",
            FailedInitialization);
    }

    // curl -s -C 10 -d x URL and -C 10 -F a=b -s URL: exit 2 and nothing printed.
    [TestMethod]
    [DataRow(new[] { "-s", "-C", "10", "-d", "x", Url })]
    [DataRow(new[] { "-C", "10", "-F", "a=b", "-s", Url })]
    public void Parse_OffsetWithBodyAndSilent_PrintsNothing(string[] arguments)
    {
        AssertRefusedAtTransferSetup(Parse(arguments));
    }

    // curl -C 10 -d x -F a=b URL: the form-and-data warning wins.
    [TestMethod]
    public void Parse_OffsetWithFormAndData_GivesTheFormAndDataRefusal()
    {
        CommandLineParseResult result = Parse(["-C", "10", "-d", "x", "-F", "a=b", Url]);

        TestDiagnostics.For(TestContext).Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
        Assert.StartsWith("Warning: You can only select one HTTP request method!", result.Refusal.StandardErrorLines[0]);
    }

    // curl -C 0 -d x, -C - -F a=b, -C - -d x, -C 10 -G -d x: accepted, curl goes on to connect.
    [TestMethod]
    [DataRow(new[] { "-C", "0", "-d", "x", Url })]
    [DataRow(new[] { "-C", "-", "-F", "a=b", Url })]
    [DataRow(new[] { "-C", "-", "-d", "x", Url })]
    [DataRow(new[] { "-C", "10", "-G", "-d", "x", Url })]
    [DataRow(new[] { "-C", "10", Url })]
    public void Parse_NoOffsetOrNoPostBody_IsAccepted(string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted, result.Refusal is null ? string.Empty : string.Join('\n', result.Refusal.StandardErrorLines));
    }

    // curl URL --next -C 10 -d x URL: the first group runs, then the second is refused.
    [TestMethod]
    public void Parse_OffsetWithDataInALaterGroup_RunsTheEarlierGroupFirst()
    {
        CommandLineParseResult result = Parse([Url, "--next", "-C", "10", "-d", "x", Url]);

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Act("refusal after groups", CommandLineParseDiagnostics.QuoteEach(result.RefusalAfterGroups?.StandardErrorLines ?? []));
        diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNotNull(result.RefusalAfterGroups);
        CollectionAssert.AreEqual(
            new[] { "curl: cannot mix --continue-at with --data", FailedInitialization },
            result.RefusalAfterGroups.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void ContinueAtWithBody_NullBodyOption_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("body option", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.ContinueAtWithBody(null!, false));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefusedAtTransferSetup(CommandLineParseResult result, params string[] refusalLines)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("accepted", false, result.IsAccepted);
        diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        diagnostics.Assert("stderr lines", CommandLineParseDiagnostics.QuoteEach(refusalLines), CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        Assert.IsTrue(result.Refusal.FoundAtTransferSetup);
        CollectionAssert.AreEqual(refusalLines, result.Refusal.StandardErrorLines.ToArray());
    }
}
