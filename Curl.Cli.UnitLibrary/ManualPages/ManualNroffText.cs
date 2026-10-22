using System.Globalization;
using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>Small text rules of upstream's <c>scripts/managen</c>: <c>manpageify</c>, <c>too_old</c> and <c>maybespace</c>.</summary>
public static class ManualNroffText
{
    /// <summary>An option's long name as the page writes it: <c>\fI\-\-name\fP</c> in nroff, <c>--name</c> in ASCII, a trailing dot kept outside.</summary>
    /// <param name="name">The long name, perhaps with a trailing dot.</param>
    /// <param name="manpage">Whether the page is nroff.</param>
    /// <returns>The written name.</returns>
    public static string Manpageify(string name, bool manpage)
    {
        string trail = name.EndsWith('.') ? "." : "";
        string bare = name[..(name.Length - trail.Length)];
        return manpage ? $"\\fI\\-\\-{bare.Replace("-", "\\-", StringComparison.Ordinal)}\\fP{trail}" : $"--{bare}{trail}";
    }

    /// <summary>Whether <paramref name="version"/> is before 7.66.0, too old for the page to mention.</summary>
    /// <param name="version">A version such as <c>7.20.2</c> or <c>4.0</c>.</param>
    /// <returns><see langword="true"/> when too old.</returns>
    public static bool IsTooOld(string version)
    {
        double value = 999999;
        Match three = Regex.Match(version, @"^([0-9]+)\.([0-9]+)\.([0-9]+)", RegexOptions.CultureInvariant);
        Match two = Regex.Match(version, @"^([0-9]+)\.([0-9]+)", RegexOptions.CultureInvariant);
        if (three.Success)
        {
            value = (Number(three, 1) * 1000) + (Number(three, 2) * 10) + Number(three, 3);
        }
        else if (two.Success)
        {
            value = (Number(two, 1) * 1000) + (Number(two, 2) * 10);
        }

        return value < 7660;
    }

    /// <summary>Where a long ASCII example line breaks: after its last space, else its last colon, when 20 characters or fewer follow.</summary>
    /// <param name="text">The first part of the example.</param>
    /// <returns>The part before the break.</returns>
    public static string MaybeSpace(string text)
    {
        foreach (string pattern in new[] { "^(.* )(.*)", "^(.*:)(.*)" })
        {
            Match match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
            if (match.Success && match.Groups[2].Length <= 20)
            {
                return match.Groups[1].Value;
            }
        }

        return text;
    }

    private static double Number(Match match, int group) => double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
}
