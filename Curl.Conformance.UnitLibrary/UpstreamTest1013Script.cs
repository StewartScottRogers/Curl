using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Emulates upstream's <c>tests/libtest/test1013.pl</c> (curl 8.22.0), which cases 1013 and 1014 run
/// as a postcheck to compare <c>curl --version</c>'s <c>Protocols:</c> or <c>Features:</c> line with
/// <c>curl-config --protocols</c> or <c>--features</c>, with <see cref="UpstreamCurlConfig"/> in place of
/// the <c>curl-config</c> script it names.
/// </summary>
/// <remarks>
/// The script takes the <c>curl-config</c> path, the file <c>curl --version</c> wrote and
/// <c>protocols</c> or <c>features</c>; any other count prints the usage and exits 3. It reads the last
/// line of the file naming the list (ignoring case), takes the words after its label, lower-cases
/// <c>curl-config</c>'s protocols, sorts both features lists, and exits 1 with a <c>Mismatch</c>
/// report when they differ. A missing file is Perl's <c>die</c>, exit 2 (<c>ENOENT</c>). A trailing
/// <c>&gt; FILE</c> redirect writes the output to FILE instead.
/// </remarks>
internal static class UpstreamTest1013Script
{
    private const int NoSuchFile = 2;

    /// <summary>The file name of the upstream script this class emulates.</summary>
    public const string ScriptName = "test1013.pl";

    private static readonly Regex ListLine = new(@"\w+: (.*)$", RegexOptions.CultureInvariant);

    /// <summary>Runs a <c>%PERL</c> line when its program is <c>test1013.pl</c>.</summary>
    /// <param name="arguments">The expanded line after the Perl program's name, starting with the script's path.</param>
    /// <returns>What the script did, or <see langword="null"/> when the line runs another program.</returns>
    public static UpstreamPerlOneLinerResult? Run(string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        (string[] words, string? redirect) = UpstreamCurlConfigScriptLine.Split(arguments);
        if (words.Length == 0 || Path.GetFileName(words[0]) != ScriptName)
        {
            return null;
        }

        UpstreamPerlOneLinerResult result = words.Length != 4
            ? new(3, $"Usage: {words[0]} curl-config-script curl-version-output-file features|protocols\n")
            : Compare(words[2], words[3]);
        return UpstreamCurlConfigScriptLine.Redirect(result, redirect);
    }

    private static UpstreamPerlOneLinerResult Compare(string versionFile, string what)
    {
        if (!File.Exists(versionFile))
        {
            return new(NoSuchFile, "");
        }

        string[] curl = CurlList(File.ReadAllLines(versionFile), what);
        string[] curlConfig = [.. UpstreamCurlConfig.Lines($"--{what}").Select(line => what == "protocols" ? line.ToLowerInvariant() : line)];
        if (what == "features")
        {
            Array.Sort(curl, StringComparer.Ordinal);
            Array.Sort(curlConfig, StringComparer.Ordinal);
        }

        string curlList = string.Join(' ', curl);
        string curlConfigList = string.Join(' ', curlConfig);
        return curlList == curlConfigList
            ? new(0, "")
            : new(1, $"Mismatch in {what} lists:\ncurl:        {curlList}\ncurl-config: {curlConfigList}\n");
    }

    private static string[] CurlList(string[] lines, string what)
    {
        string line = lines.LastOrDefault(line => line.Contains($"{what}:", StringComparison.OrdinalIgnoreCase), "").Replace("\r", "", StringComparison.Ordinal);
        Match list = ListLine.Match(line);
        return list.Success ? list.Groups[1].Value.Split(' ').Reverse().SkipWhile(word => word.Length == 0).Reverse().ToArray() : [];
    }
}
