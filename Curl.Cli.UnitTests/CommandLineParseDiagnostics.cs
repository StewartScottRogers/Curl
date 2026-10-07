using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Writes a parser test's diagnostics through <see cref="TestDiagnostics"/> (BL-1459): the argument array
/// it parses, each argument quoted so spaces and empty arguments show, and what the parser made of it -
/// accepted or refused, the refusal's exit code and standard error lines, and any warning lines.
/// </summary>
internal static class CommandLineParseDiagnostics
{
    /// <summary>Writes <paramref name="arguments"/> as one <c>ARRANGE arguments</c> line, each argument in double quotes.</summary>
    public static void ArrangeArguments(this TestDiagnostics diagnostics, IReadOnlyList<string> arguments) =>
        diagnostics.Arrange("arguments", QuoteEach(arguments));

    /// <summary>
    /// Writes <paramref name="result"/> as <c>ACT</c> lines: whether it was accepted, then for a refusal its exit
    /// code and every standard error line, and every warning line.
    /// </summary>
    public static void ActParse(this TestDiagnostics diagnostics, CommandLineParseResult result)
    {
        diagnostics.Act("accepted", result.IsAccepted);
        if (result.Refusal is { } refusal)
        {
            diagnostics.Act("exit code", $"{(int)refusal.ExitCode} ({refusal.ExitCode})");
            foreach (string line in refusal.StandardErrorLines)
            {
                diagnostics.Act("stderr", line);
            }
        }

        foreach (string line in result.WarningLines)
        {
            diagnostics.Act("warning", line);
        }
    }

    /// <summary>Formats <paramref name="values"/> as a bracketed list, each value in double quotes, so spaces and empty values show.</summary>
    public static string QuoteEach(IEnumerable<string?> values) =>
        "[" + string.Join(", ", values.Select(value => value is null ? "null" : "\"" + value + "\"")) + "]";
}
