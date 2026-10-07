using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the warnings curl 8.21.0 prints when <c>-L</c>/<c>--location</c> (or <c>--location-trusted</c>) and
/// <c>--follow</c> replace each other, measured with the local curl 8.21.0 on 2026-10-02 (BL-1223): each
/// spelling, <c>--no-</c> ones included, warns only when the other was in force, and <c>-s</c> read
/// before it hides the warning.
/// </summary>
[TestClass]
public sealed class CommandLineLocationFollowOverrideTests
{
    private const string Url = "file:///nonexist";

    private const string LocationOverridesFollow = "Warning: --location overrides --follow";

    private const string FollowOverridesLocation = "Warning: --follow overrides --location";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(new[] { "-L", "--follow", Url }, new[] { FollowOverridesLocation })]
    [DataRow(new[] { "--follow", "-L", Url }, new[] { LocationOverridesFollow })]
    [DataRow(new[] { "--follow", "--location", Url }, new[] { LocationOverridesFollow })]
    [DataRow(new[] { "--follow", "--location-trusted", Url }, new[] { LocationOverridesFollow })]
    [DataRow(new[] { "--location-trusted", "--follow", Url }, new[] { FollowOverridesLocation })]
    [DataRow(new[] { "-L", "--no-follow", Url }, new[] { FollowOverridesLocation })]
    [DataRow(new[] { "--follow", "--no-location", Url }, new[] { LocationOverridesFollow })]
    [DataRow(new[] { "--follow", "--no-location", "-s", Url }, new[] { LocationOverridesFollow })]
    [DataRow(new[] { "-L", "--follow", "--follow", Url }, new[] { FollowOverridesLocation })]
    [DataRow(new[] { "-L", "-L", "--follow", "-L", Url }, new[] { FollowOverridesLocation, LocationOverridesFollow })]
    [DataRow(new[] { "-L", "-r", "5", "--follow", Url }, new[] { "Warning: A specified range MUST include at least one dash (-). Appending one for you", FollowOverridesLocation })]
    public void Parse_OneRedirectOptionReplacingTheOther_WarnsInOrder(string[] arguments, string[] warningLines)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(warningLines), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(warningLines, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "-s", "-L", "--follow", Url })]
    [DataRow(new[] { "-s", "--follow", "-L", Url })]
    [DataRow(new[] { "-s", "-S", "--follow", "-L", Url })]
    [DataRow(new[] { "--no-follow", "-L", Url })]
    [DataRow(new[] { "--no-location", "--follow", Url })]
    [DataRow(new[] { "-L", "-L", Url })]
    [DataRow(new[] { "--follow", "--follow", Url })]
    [DataRow(new[] { "-L", "--location-trusted", Url })]
    public void Parse_NoOverrideOrSilent_WarnsNothing(string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("warning lines", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow(new[] { "-L", "--follow", Url }, true, true)]
    [DataRow(new[] { "--follow", "-L", Url }, true, false)]
    [DataRow(new[] { "-L", "--no-follow", Url }, false, false)]
    [DataRow(new[] { "--follow", "--no-location", Url }, false, false)]
    public void Parse_OneRedirectOptionReplacingTheOther_LastOneWins(string[] arguments, bool followRedirects, bool followRedirectsPerSpec)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("follow redirects", followRedirects, result.Options?.FollowRedirects);
        Diagnostics.Assert("follow redirects per spec", followRedirectsPerSpec, result.Options?.FollowRedirectsPerSpec);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(followRedirects, result.Options.FollowRedirects);
        Assert.AreEqual(followRedirectsPerSpec, result.Options.FollowRedirectsPerSpec);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
