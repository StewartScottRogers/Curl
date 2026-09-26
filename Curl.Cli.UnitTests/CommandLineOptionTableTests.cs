namespace Curl.Cli;

/// <summary>
/// Pins the option table the parser reads: every long name and every short letter
/// appears once, and the first table holds <c>--url</c>, <c>-s</c>/<c>--silent</c>,
/// <c>-S</c>/<c>--show-error</c> and <c>-o</c>/<c>--output</c> with the right arity,
/// and its two text options refuse an empty value as blank.
/// </summary>
[TestClass]
public sealed class CommandLineOptionTableTests
{
    [TestMethod]
    public void Rows_LongNames_AreUnique()
    {
        string[] longNames = CommandLineOptionTable.Rows.Select(option => option.LongName).ToArray();

        CollectionAssert.AllItemsAreUnique(longNames);
    }

    [TestMethod]
    public void Rows_ShortNames_AreUnique()
    {
        char[] shortNames = CommandLineOptionTable.Rows
            .Where(option => option.ShortName.HasValue)
            .Select(option => option.ShortName!.Value)
            .ToArray();

        CollectionAssert.AllItemsAreUnique(shortNames);
    }

    [TestMethod]
    [DataRow("url", null, true)]
    [DataRow("silent", 's', false)]
    [DataRow("show-error", 'S', false)]
    [DataRow("output", 'o', true)]
    public void Rows_FirstTableOption_HasItsShortNameAndArity(string longName, char? shortName, bool takesValue)
    {
        CommandLineOption option = CommandLineOptionTable.Rows.Single(row => row.LongName == longName);

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

        CommandLineRefusal? refusal = option.Apply(options, string.Empty, spelledOption);

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
        CommandLineOption option = CommandLineOptionTable.Rows.Single(row => row.LongName == "url");
        CommandLineOptions options = new();

        CommandLineRefusal? refusal = option.Apply(options, "http://example.com", "--url");

        Assert.IsNull(refusal);
        CollectionAssert.AreEqual(new[] { "http://example.com" }, options.Urls.ToArray());
    }

    [TestMethod]
    public void Rows_OutputOptionGivenValue_AddsOutputFile()
    {
        CommandLineOption option = CommandLineOptionTable.Rows.Single(row => row.LongName == "output");
        CommandLineOptions options = new();

        CommandLineRefusal? refusal = option.Apply(options, "page.html", "-o");

        Assert.IsNull(refusal);
        CollectionAssert.AreEqual(new[] { "page.html" }, options.OutputFiles.ToArray());
    }
}
