using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins ADR-0137: a name or letter curl 8.21.0 knows (<see cref="CurlOptionAliasTable"/>) that
/// <see cref="CommandLineOptionTable"/> has no row for is refused with
/// <c>curl: option &lt;spelled&gt;: the installed libcurl version does not support this</c>, exit 2, and a
/// name in neither table keeps <c>is unknown</c>, exit 2. The unimplemented set is derived from the two
/// tables, never listed, so these tests follow it as options gain rows. Measured with the Windows system
/// curl 8.21.0 (Schannel) on 2026-09-28 for <c>--http3</c> (ADR-0137's Context); <c>-s</c> hides neither line.
/// </summary>
[TestClass]
public sealed class CommandLineUnimplementedOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string ErrorPrefix = "curl: ";

    private const string NotSupported = "the installed libcurl version does not support this";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static IEnumerable<CurlOptionAlias> UnimplementedAliases =>
        CurlOptionAliasTable.Aliases.Where(alias => !CommandLineOptionTable.Rows.Any(row => row.LongName == alias.Name));

    private static IEnumerable<char> UnimplementedLetters =>
        CurlOptionAliasTable.Aliases
            .Where(alias => alias.Letter != CurlOptionAliasTable.NoLetter && !CommandLineOptionTable.Rows.Any(row => row.ShortName == alias.Letter))
            .Select(alias => alias.Letter);

    [TestMethod]
    public void Parse_EveryAliasWithoutARow_IsRefusedAsNotSupported()
    {
        int unimplementedAliasCount = ArrangeUnimplementedAliases();
        int checkedAliasCount = 0;

        foreach (CurlOptionAlias alias in UnimplementedAliases)
        {
            checkedAliasCount++;
            AssertRefused(Parse([$"--{alias.Name}", Url]), $"curl: option --{alias.Name}: {NotSupported}");
        }

        AssertAliasesChecked(unimplementedAliasCount, checkedAliasCount);
    }

    /// <summary>
    /// The parser refuses a letter without a row as unknown (BL-1421), which is right only while every one of
    /// curl 8.21.0's letters has a row; this fails, naming the letter, should one ever be removed.
    /// </summary>
    [TestMethod]
    public void EveryCurlLetter_HasARow()
    {
        Diagnostics.Arrange("curl letters", CurlOptionAliasTable.Aliases.Count(alias => alias.Letter != CurlOptionAliasTable.NoLetter));
        string unimplementedLetters = string.Concat(UnimplementedLetters);
        Diagnostics.Act("letters without a row", "\"" + unimplementedLetters + "\"");

        Diagnostics.Assert("letters without a row", "\"\"", "\"" + unimplementedLetters + "\"");
        Assert.AreEqual(string.Empty, unimplementedLetters);
    }

    [TestMethod]
    public void Parse_EveryAliasWithoutARowExpanded_IsRefusedAsNotSupported()
    {
        int unimplementedAliasCount = ArrangeUnimplementedAliases();
        int checkedAliasCount = 0;

        foreach (CurlOptionAlias alias in UnimplementedAliases)
        {
            checkedAliasCount++;
            AssertRefused(Parse([$"--expand-{alias.Name}", "x", Url]), $"curl: option --expand-{alias.Name}: {NotSupported}");
        }

        AssertAliasesChecked(unimplementedAliasCount, checkedAliasCount);
    }

    [TestMethod]
    public void Parse_EveryAliasWithoutARowNegated_IsRefusedAsTheAliasTableSays()
    {
        int unimplementedAliasCount = ArrangeUnimplementedAliases();
        int checkedAliasCount = 0;

        foreach (CurlOptionAlias alias in UnimplementedAliases)
        {
            checkedAliasCount++;
            string reason = alias.NoPrefix == CurlOptionNoPrefix.NotAccepted
                ? "the given option cannot be reversed with a --no- prefix"
                : NotSupported;
            AssertRefused(Parse([$"--no-{alias.Name}", Url]), $"curl: option --no-{alias.Name}: {reason}");
        }

        AssertAliasesChecked(unimplementedAliasCount, checkedAliasCount);
    }

    [TestMethod]
    public void Parse_EveryAlias_HasARowOrIsNeverRefusedAsUnknown()
    {
        Diagnostics.Arrange("aliases", CurlOptionAliasTable.Aliases.Count);
        List<string> refusedAsUnknown = [];

        foreach (CurlOptionAlias alias in CurlOptionAliasTable.Aliases)
        {
            CommandLineParseResult result = CommandLineParser.Parse([$"--{alias.Name}"]);
            bool isUnknown = result.Refusal?.StandardErrorLines.Any(line => line.EndsWith(": is unknown", StringComparison.Ordinal)) ?? false;
            if (isUnknown)
            {
                refusedAsUnknown.Add(alias.Name);
                Diagnostics.Act("refused as unknown", alias.Name);
            }

            Assert.IsFalse(
                result.Refusal?.StandardErrorLines.Any(line => line.EndsWith(": is unknown", StringComparison.Ordinal)) ?? false,
                alias.Name);
        }

        Diagnostics.Act("aliases refused as unknown", refusedAsUnknown.Count);
        Diagnostics.Assert("aliases refused as unknown", CommandLineParseDiagnostics.QuoteEach([]), CommandLineParseDiagnostics.QuoteEach(refusedAsUnknown));
    }

    [TestMethod]
    [DataRow("--bogus", "--bogus")]
    [DataRow("-9", "-9")]
    [DataRow("--no-bogus", "--no-bogus")]
    [DataRow("--expand-bogus", "--expand-bogus")]
    [DataRow("--expand-no-silent", "--expand-no-silent")]
    public void Parse_NameInNeitherTable_IsStillRefusedAsUnknown(string argument, string spelled)
    {
        AssertRefused(Parse([argument, Url]), $"curl: option {spelled}: is unknown");
    }

    [TestMethod]
    public void Parse_UnimplementedOptionAfterSilent_IsStillRefusedAsNotSupported()
    {
        int unimplementedAliasCount = ArrangeUnimplementedAliases();
        int checkedAliasCount = 0;

        foreach (CurlOptionAlias alias in UnimplementedAliases.Take(1))
        {
            checkedAliasCount++;
            AssertRefused(Parse(["-s", $"--{alias.Name}", Url]), $"curl: option --{alias.Name}: {NotSupported}");
        }

        AssertAliasesChecked(Math.Min(1, unimplementedAliasCount), checkedAliasCount);
    }

    /// <summary>
    /// A <c>-K</c> line naming an unimplemented option: the line's own line with curl's reason, then
    /// <c>curl: option -K: the installed libcurl version does not support this</c>, exit 2 (ADR-0137). The
    /// line's own line is wrapped at 79 columns, each piece after the first starting <c>curl: </c> again, so
    /// the test joins the pieces back before comparing.
    /// </summary>
    [TestMethod]
    public void Parse_ConfigFileLineNamingAnUnimplementedOption_IsRefusedAsNotSupported()
    {
        int unimplementedAliasCount = ArrangeUnimplementedAliases();
        int checkedAliasCount = 0;

        foreach (CurlOptionAlias alias in UnimplementedAliases)
        {
            checkedAliasCount++;
            CommandLineParseResult result = ParseConfigFile($"{alias.Name}\n");

            Diagnostics.Assert("exit code", (int)CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal) is { } refusal ? (int)refusal.ExitCode : null);
            Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode, alias.Name);
            string[] lines = result.Refusal.StandardErrorLines.ToArray();
            Diagnostics.Assert(
                "joined first line",
                $"curl: c.cfg:1 config file option '{alias.Name}' {NotSupported}",
                lines[0] + string.Concat(lines[1..^2].Select(piece => piece[ErrorPrefix.Length..])));
            Assert.AreEqual(
                $"curl: c.cfg:1 config file option '{alias.Name}' {NotSupported}",
                lines[0] + string.Concat(lines[1..^2].Select(piece => piece[ErrorPrefix.Length..])),
                string.Join("|", lines));
            Diagnostics.Assert(
                "last two lines",
                CommandLineParseDiagnostics.QuoteEach([$"curl: option -K: {NotSupported}", CommandLineRefusal.TryHelpLine]),
                CommandLineParseDiagnostics.QuoteEach(lines[^2..]));
            CollectionAssert.AreEqual(new[] { $"curl: option -K: {NotSupported}", CommandLineRefusal.TryHelpLine }, lines[^2..], alias.Name);
        }

        AssertAliasesChecked(unimplementedAliasCount, checkedAliasCount);
    }

    [TestMethod]
    public void Parse_ConfigFileLineNamingNoCurlOption_IsStillAnUnknownConfigOption()
    {
        CommandLineParseResult result = ParseConfigFile("bogus\n");

        string[] expected =
        [
            "curl: c.cfg:1 config file option 'bogus' is unknown",
            "curl: option -K: found an unknown config option",
            CommandLineRefusal.TryHelpLine,
        ];
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expected),
            CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: c.cfg:1 config file option 'bogus' is unknown",
                "curl: option -K: found an unknown config option",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    private int ArrangeUnimplementedAliases()
    {
        string[] names = [.. UnimplementedAliases.Select(alias => alias.Name)];
        Diagnostics.Arrange("unimplemented aliases", CommandLineParseDiagnostics.QuoteEach(names));
        return names.Length;
    }

    /// <summary>
    /// Writes how many aliases the loop checked, so a test whose alias set is empty still shows an <c>ACT</c> and an
    /// <c>ASSERT</c> line saying it checked none.
    /// </summary>
    private void AssertAliasesChecked(int expected, int checkedAliasCount)
    {
        Diagnostics.Act("aliases checked", checkedAliasCount);
        Diagnostics.Assert("aliases checked", expected, checkedAliasCount);
    }

    /// <summary>Parses <paramref name="arguments"/>, writing them and the outcome as diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    /// <summary>Parses <c>-K c.cfg</c> with <paramref name="text"/> as the file, writing the text and the outcome as diagnostics.</summary>
    private CommandLineParseResult ParseConfigFile(string text)
    {
        RecordingDataFileReader reader = new();
        reader.Files["c.cfg"] = Encoding.UTF8.GetBytes(text);
        string[] arguments = ["-K", "c.cfg", Url];
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Bytes("c.cfg", reader.Files["c.cfg"]);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [expectedFirstLine, CommandLineRefusal.TryHelpLine]);
        Assert.IsFalse(result.IsAccepted, expectedFirstLine);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode, expectedFirstLine);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray(),
            expectedFirstLine);
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
