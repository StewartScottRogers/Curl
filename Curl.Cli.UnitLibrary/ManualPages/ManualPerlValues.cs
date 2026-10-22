using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>The few Perl value rules upstream's <c>scripts/managen</c> leans on: truth, <c>split</c>, numbers and its <c>:crlf</c> lines.</summary>
public static class ManualPerlValues
{
    /// <summary>Whether Perl reads <paramref name="value"/> as true: defined, not empty and not <c>"0"</c>.</summary>
    public static bool IsTrue([NotNullWhen(true)] string? value) => !string.IsNullOrEmpty(value) && value != "0";

    /// <summary>Perl's <c>split</c> with a pattern: a leading empty field is kept, trailing empty fields are dropped.</summary>
    public static List<string> Split(string text, string pattern)
    {
        List<string> fields = [.. Regex.Split(text, pattern, RegexOptions.CultureInvariant)];
        while (fields.Count > 0 && fields[^1].Length == 0)
        {
            fields.RemoveAt(fields.Count - 1);
        }

        return fields;
    }

    /// <summary>Perl's numeric value of a string: its leading integer, or 0 when it starts with none.</summary>
    public static int Number(string text)
    {
        Match number = Regex.Match(text, "^-?[0-9]+", RegexOptions.CultureInvariant);
        return number.Success ? int.Parse(number.Value, CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>A file's lines as <c>open(..., "&lt;:crlf", ...)</c> reads them: CR LF read as LF, each line keeping its LF.</summary>
    public static List<string> Lines(string text)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        List<string> lines = [];
        int start = 0;
        for (int end = normalized.IndexOf('\n', start); end >= 0; end = normalized.IndexOf('\n', start))
        {
            lines.Add(normalized[start..(end + 1)]);
            start = end + 1;
        }

        if (start < normalized.Length)
        {
            lines.Add(normalized[start..]);
        }

        return lines;
    }

    /// <summary>Perl's <c>chomp</c>: the text without one trailing line feed.</summary>
    public static string Chomp(string text) => text.EndsWith('\n') ? text[..^1] : text;
}
