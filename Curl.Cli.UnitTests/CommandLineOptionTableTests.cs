using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the option table the parser reads: every long name and every short letter
/// appears once, the table holds <c>--url</c>, <c>-s</c>/<c>--silent</c>,
/// <c>-S</c>/<c>--show-error</c>, <c>-o</c>/<c>--output</c>, <c>-d</c>/<c>--data</c>,
/// <c>-u</c>/<c>--user</c>, <c>-t</c>/<c>--telnet-option</c>, <c>--tftp-blksize</c>,
/// <c>--tftp-no-options</c>, <c>--create-file-mode</c> and the TLS options with the right arity,
/// and its two text options refuse an empty value as blank.
/// </summary>
[TestClass]
public sealed class CommandLineOptionTableTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Rows_LongNames_AreUnique()
    {
        Diagnostics.Arrange("table", "CommandLineOptionTable.Rows");
        string[] longNames = CommandLineOptionTable.Rows.Select(option => option.LongName).ToArray();
        Diagnostics.Act("long names", longNames.Length);

        Diagnostics.Assert("distinct long names", longNames.Length, longNames.Distinct(StringComparer.Ordinal).Count());
        CollectionAssert.AllItemsAreUnique(longNames);
    }

    [TestMethod]
    public void Rows_ShortNames_AreUnique()
    {
        Diagnostics.Arrange("table", "CommandLineOptionTable.Rows");
        char[] shortNames = CommandLineOptionTable.Rows
            .Where(option => option.ShortName.HasValue)
            .Select(option => option.ShortName!.Value)
            .ToArray();
        Diagnostics.Act("short names", new string(shortNames));

        Diagnostics.Assert("distinct short names", shortNames.Length, shortNames.Distinct().Count());
        CollectionAssert.AllItemsAreUnique(shortNames);
    }

    [TestMethod]
    [DataRow("url", null, true)]
    [DataRow("globoff", 'g', false)]
    [DataRow("silent", 's', false)]
    [DataRow("show-error", 'S', false)]
    [DataRow("progress-meter", null, false)]
    [DataRow("progress-bar", '#', false)]
    [DataRow("output", 'o', true)]
    [DataRow("data", 'd', true)]
    [DataRow("dump-header", 'D', true)]
    [DataRow("user", 'u', true)]
    [DataRow("telnet-option", 't', true)]
    [DataRow("tftp-blksize", null, true)]
    [DataRow("resolve", null, true)]
    [DataRow("connect-to", null, true)]
    [DataRow("tftp-no-options", null, false)]
    [DataRow("create-file-mode", null, true)]
    [DataRow("insecure", 'k', false)]
    [DataRow("cacert", null, true)]
    [DataRow("capath", null, true)]
    [DataRow("crlfile", null, true)]
    [DataRow("pinnedpubkey", null, true)]
    [DataRow("cert-status", null, false)]
    [DataRow("ssl-auto-client-cert", null, false)]
    [DataRow("proxy-crlfile", null, true)]
    [DataRow("proxy-ca-native", null, false)]
    [DataRow("cert", 'E', true)]
    [DataRow("key", null, true)]
    [DataRow("cert-type", null, true)]
    [DataRow("key-type", null, true)]
    [DataRow("pass", null, true)]
    [DataRow("tlsv1.2", null, false)]
    [DataRow("tlsv1.3", null, false)]
    [DataRow("ciphers", null, true)]
    [DataRow("tls13-ciphers", null, true)]
    [DataRow("remote-time", 'R', false)]
    [DataRow("request", 'X', true)]
    [DataRow("header", 'H', true)]
    [DataRow("user-agent", 'A', true)]
    [DataRow("referer", 'e', true)]
    [DataRow("cookie", 'b', true)]
    [DataRow("cookie-jar", 'c', true)]
    [DataRow("junk-session-cookies", 'j', false)]
    public void Rows_FirstTableOption_HasItsShortNameAndArity(string longName, char? shortName, bool takesValue)
    {
        Diagnostics.Arrange("long name", longName);
        CommandLineOption option = CommandLineOptionTable.Rows.Single(row => row.LongName == longName);
        Diagnostics.Act("short name", option.ShortName);
        Diagnostics.Act("takes value", option.TakesValue);

        Diagnostics.Assert("short name", shortName, option.ShortName);
        Diagnostics.Assert("takes value", takesValue, option.TakesValue);
        Assert.AreEqual(shortName, option.ShortName);
        Assert.AreEqual(takesValue, option.TakesValue);
    }

    [TestMethod]
    [DataRow("url", "--url")]
    [DataRow("output", "-o")]
    public void Rows_TextOptionGivenBlankValue_RefusesAsBlankNamingSpelledOption(string longName, string spelledOption)
    {
        CommandLineOption option = CommandLineOptionTable.Rows.Single(row => row.LongName == longName);
        CommandLineOptions options = new();

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([string.Empty]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach([spelledOption]));
        CommandLineRefusal? refusal = option.Apply(options, string.Empty, spelledOption, _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);
        Diagnostics.Act("urls", CommandLineParseDiagnostics.QuoteEach(options.Urls));
        Diagnostics.Act("output files", CommandLineParseDiagnostics.QuoteEach(options.OutputFiles));

        Diagnostics.Assert("refused", true, refusal is not null);
        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: blank argument where content is expected", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Assert.IsEmpty(options.Urls);
        Assert.IsEmpty(options.OutputFiles);
    }

    [TestMethod]
    public void Rows_UrlOptionGivenValue_AddsUrl()
    {
        Diagnostics.Arrange("long name", "url");
        CommandLineOption option = CommandLineOptionTable.Rows.Single(row => row.LongName == "url");
        CommandLineOptions options = new();

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach(["http://example.com"]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["--url"]));
        CommandLineRefusal? refusal = option.Apply(options, "http://example.com", "--url", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);
        Diagnostics.Act("urls", CommandLineParseDiagnostics.QuoteEach(options.Urls));
        Diagnostics.Act("output files", CommandLineParseDiagnostics.QuoteEach(options.OutputFiles));

        Diagnostics.Assert("refused", false, refusal is not null);
        Assert.IsNull(refusal);
        CollectionAssert.AreEqual(new[] { "http://example.com" }, options.Urls.ToArray());
    }

    [TestMethod]
    public void Rows_OutputOptionGivenValue_AddsOutputFile()
    {
        Diagnostics.Arrange("long name", "output");
        CommandLineOption option = CommandLineOptionTable.Rows.Single(row => row.LongName == "output");
        CommandLineOptions options = new();

        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach(["page.html"]));
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach(["-o"]));
        CommandLineRefusal? refusal = option.Apply(options, "page.html", "-o", _ => false, new RecordingDataFileReader());
        ActRefusal(refusal);
        Diagnostics.Act("urls", CommandLineParseDiagnostics.QuoteEach(options.Urls));
        Diagnostics.Act("output files", CommandLineParseDiagnostics.QuoteEach(options.OutputFiles));

        Diagnostics.Assert("refused", false, refusal is not null);
        Assert.IsNull(refusal);
        CollectionAssert.AreEqual(new[] { "page.html" }, options.OutputFiles.ToArray());
    }

    private void ActRefusal(CommandLineRefusal? refusal)
    {
        Diagnostics.Act("refused", refusal is not null);
        foreach (string line in refusal?.StandardErrorLines ?? [])
        {
            Diagnostics.Act("stderr", line);
        }
    }
}
