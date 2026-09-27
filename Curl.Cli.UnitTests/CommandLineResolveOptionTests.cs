using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--resolve</c> and <c>--connect-to</c>: every value verbatim, in
/// command-line order, never refused, because curl 8.21.0 reads these entries only when a transfer
/// starts. Measured with the local curl 8.21.0 (mingw, Schannel) on 2026-09-26 against
/// <c>http://127.0.0.1:1/</c>: <c>--resolve ''</c>, <c>--resolve '[::1]:80:127.0.0.1'</c>,
/// <c>'+a:80:127.0.0.1'</c>, <c>'*:80:127.0.0.1'</c>, <c>'-a:80'</c> and <c>'a:80:127.0.0.1,[::1]'</c>
/// went on to connect (exit 7 or 28); <c>--resolve garbage</c>, <c>'a:x:1.2.3.4'</c> and <c>'a:80:'</c>
/// failed at transfer time with exit 49 and <c>curl: (49) Could not parse CURLOPT_RESOLVE entry 'garbage'</c>,
/// not with a parse-time refusal; <c>--connect-to</c> with <c>''</c> or <c>garbage</c> went on to connect.
/// Only a missing value and the <c>--no-</c> spellings are refused while parsing, with exit 2.
/// </summary>
[TestClass]
public sealed class CommandLineResolveOptionTests
{
    private const string Url = "http://example.com/";

    [TestMethod]
    public void Parse_NoResolveOrConnectTo_RecordsNoEntries()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.Options.ResolveEntries);
        Assert.IsEmpty(result.Options.ConnectToEntries);
    }

    [TestMethod]
    [DataRow("example.com:80:127.0.0.1")]
    [DataRow("*:80:127.0.0.1")]
    [DataRow("+example.com:443:127.0.0.1")]
    [DataRow("-example.com:80")]
    [DataRow("[::1]:80:127.0.0.1")]
    [DataRow("example.com:80:[::1]")]
    [DataRow("example.com:80:127.0.0.1,[::1],10.0.0.1")]
    [DataRow("garbage")]
    [DataRow("a:x:1.2.3.4")]
    [DataRow("a:80:")]
    [DataRow("")]
    public void Parse_Resolve_RecordsTheValueVerbatimAndNeverRefuses(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--resolve", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { value }, result.Options.ResolveEntries.ToArray());
    }

    [TestMethod]
    [DataRow("example.com:80:127.0.0.1:8080")]
    [DataRow("::127.0.0.1:")]
    [DataRow("[::1]:80:[fe80::1]:8080")]
    [DataRow("example.com::other.example:")]
    [DataRow("garbage")]
    [DataRow("")]
    public void Parse_ConnectTo_RecordsTheValueVerbatimAndNeverRefuses(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--connect-to", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { value }, result.Options.ConnectToEntries.ToArray());
    }

    [TestMethod]
    public void Parse_SeveralResolveAndConnectToValues_RecordsEachInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
        [
            "--resolve", "a:80:1.1.1.1",
            "--connect-to", "a:80:b:81",
            "--resolve", "*:443:[::1]",
            "--connect-to=::c:",
            "--resolve=-a:80",
            Url,
        ]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "a:80:1.1.1.1", "*:443:[::1]", "-a:80" }, result.Options.ResolveEntries.ToArray());
        CollectionAssert.AreEqual(new[] { "a:80:b:81", "::c:" }, result.Options.ConnectToEntries.ToArray());
    }

    [TestMethod]
    [DataRow("--resolve")]
    [DataRow("--connect-to")]
    public void Parse_ResolveOrConnectToWithoutAValue_RefusesAsRequiringAParameter(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, spelledOption]);

        AssertRefused(result, $"curl: option {spelledOption}: requires parameter");
    }

    [TestMethod]
    public void Parse_NoResolve_RefusesAsNotReversible()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-resolve", Url]);

        AssertRefused(result, "curl: option --no-resolve: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    public void Parse_NoConnectTo_RefusesAsNotReversible()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-connect-to", Url]);

        AssertRefused(result, "curl: option --no-connect-to: the given option cannot be reversed with a --no- prefix");
    }

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
