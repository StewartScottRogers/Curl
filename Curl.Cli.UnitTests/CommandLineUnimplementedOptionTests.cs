using System.Text;
using Curl.Protocol.Abstractions;

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

    private static IEnumerable<CurlOptionAlias> UnimplementedAliases =>
        CurlOptionAliasTable.Aliases.Where(alias => !CommandLineOptionTable.Rows.Any(row => row.LongName == alias.Name));

    private static IEnumerable<char> UnimplementedLetters =>
        CurlOptionAliasTable.Aliases
            .Where(alias => alias.Letter != CurlOptionAliasTable.NoLetter && !CommandLineOptionTable.Rows.Any(row => row.ShortName == alias.Letter))
            .Select(alias => alias.Letter);

    [TestMethod]
    public void Parse_EveryAliasWithoutARow_IsRefusedAsNotSupported()
    {
        foreach (CurlOptionAlias alias in UnimplementedAliases)
        {
            AssertRefused(CommandLineParser.Parse([$"--{alias.Name}", Url]), $"curl: option --{alias.Name}: {NotSupported}");
        }
    }

    [TestMethod]
    public void Parse_EveryLetterWithoutARow_IsRefusedAsNotSupported()
    {
        foreach (char letter in UnimplementedLetters)
        {
            AssertRefused(CommandLineParser.Parse([$"-{letter}", Url]), $"curl: option -{letter}: {NotSupported}");
        }
    }

    [TestMethod]
    public void Parse_EveryLetterWithoutARowInABundle_IsRefusedSpelledAsTheWholeArgument()
    {
        foreach (char letter in UnimplementedLetters)
        {
            AssertRefused(CommandLineParser.Parse([$"-s{letter}", Url]), $"curl: option -s{letter}: {NotSupported}");
        }
    }

    [TestMethod]
    public void Parse_EveryAliasWithoutARowExpanded_IsRefusedAsNotSupported()
    {
        foreach (CurlOptionAlias alias in UnimplementedAliases)
        {
            AssertRefused(CommandLineParser.Parse([$"--expand-{alias.Name}", "x", Url]), $"curl: option --expand-{alias.Name}: {NotSupported}");
        }
    }

    [TestMethod]
    public void Parse_EveryAliasWithoutARowNegated_IsRefusedAsTheAliasTableSays()
    {
        foreach (CurlOptionAlias alias in UnimplementedAliases)
        {
            string reason = alias.NoPrefix == CurlOptionNoPrefix.NotAccepted
                ? "the given option cannot be reversed with a --no- prefix"
                : NotSupported;
            AssertRefused(CommandLineParser.Parse([$"--no-{alias.Name}", Url]), $"curl: option --no-{alias.Name}: {reason}");
        }
    }

    [TestMethod]
    public void Parse_EveryAlias_HasARowOrIsNeverRefusedAsUnknown()
    {
        foreach (CurlOptionAlias alias in CurlOptionAliasTable.Aliases)
        {
            CommandLineParseResult result = CommandLineParser.Parse([$"--{alias.Name}"]);
            Assert.IsFalse(
                result.Refusal?.StandardErrorLines.Any(line => line.EndsWith(": is unknown", StringComparison.Ordinal)) ?? false,
                alias.Name);
        }
    }

    [TestMethod]
    [DataRow("--bogus", "--bogus")]
    [DataRow("-9", "-9")]
    [DataRow("--no-bogus", "--no-bogus")]
    [DataRow("--expand-bogus", "--expand-bogus")]
    [DataRow("--expand-no-silent", "--expand-no-silent")]
    public void Parse_NameInNeitherTable_IsStillRefusedAsUnknown(string argument, string spelled)
    {
        AssertRefused(CommandLineParser.Parse([argument, Url]), $"curl: option {spelled}: is unknown");
    }

    [TestMethod]
    public void Parse_UnimplementedOptionAfterSilent_IsStillRefusedAsNotSupported()
    {
        foreach (CurlOptionAlias alias in UnimplementedAliases.Take(1))
        {
            AssertRefused(CommandLineParser.Parse(["-s", $"--{alias.Name}", Url]), $"curl: option --{alias.Name}: {NotSupported}");
        }
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
        foreach (CurlOptionAlias alias in UnimplementedAliases)
        {
            CommandLineParseResult result = ParseConfigFile($"{alias.Name}\n");

            Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode, alias.Name);
            string[] lines = result.Refusal.StandardErrorLines.ToArray();
            Assert.AreEqual(
                $"curl: c.cfg:1 config file option '{alias.Name}' {NotSupported}",
                lines[0] + string.Concat(lines[1..^2].Select(piece => piece[ErrorPrefix.Length..])),
                string.Join("|", lines));
            CollectionAssert.AreEqual(new[] { $"curl: option -K: {NotSupported}", CommandLineRefusal.TryHelpLine }, lines[^2..], alias.Name);
        }
    }

    [TestMethod]
    public void Parse_ConfigFileLineNamingNoCurlOption_IsStillAnUnknownConfigOption()
    {
        CommandLineParseResult result = ParseConfigFile("bogus\n");

        CollectionAssert.AreEqual(
            new[]
            {
                "curl: c.cfg:1 config file option 'bogus' is unknown",
                "curl: option -K: found an unknown config option",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    private static CommandLineParseResult ParseConfigFile(string text)
    {
        RecordingDataFileReader reader = new();
        reader.Files["c.cfg"] = Encoding.UTF8.GetBytes(text);
        return CommandLineParser.Parse(["-K", "c.cfg", Url], _ => true, new UnexpectedPasswordPrompt(), reader);
    }

    private static void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
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
