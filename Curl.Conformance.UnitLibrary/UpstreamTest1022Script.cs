using System.Globalization;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Emulates upstream's <c>tests/libtest/test1022.pl</c> (curl 8.22.0), which cases 1022 and 1023 run
/// as a postcheck to compare the libcurl version on <c>curl --version</c>'s first line with
/// <c>curl-config --version</c> or <c>--vernum</c>, with <see cref="UpstreamCurlConfig"/> in place of
/// the <c>curl-config</c> script it names.
/// </summary>
/// <remarks>
/// The script takes the <c>curl-config</c> path, the file <c>curl --version</c> wrote and
/// <c>version</c> or <c>vernum</c>; any other count prints the usage and exits 3. <c>version</c> reads
/// the version from <c>libcurl X.Y.Z</c>; <c>vernum</c> turns the six hexadecimal digits into
/// <c>X.Y.Z</c> in decimal and drops a <c>-DEV</c> or <c>-rc</c> suffix from curl's version. A value of
/// another shape is <c>illegal value</c>. A difference, or no version on curl's first line, prints a
/// <c>Mismatch</c> report and exits 1. A missing file is Perl's <c>die</c>, exit 2 (<c>ENOENT</c>).
/// </remarks>
internal static class UpstreamTest1022Script
{
    private const int NoSuchFile = 2;

    private const string IllegalValue = "illegal value";

    /// <summary>The file name of the upstream script this class emulates.</summary>
    public const string ScriptName = "test1022.pl";

    private static readonly Regex CurlVersion = new(@"libcurl/([.0-9]+((-DEV)|(-rc[0-9])|(-[0-9]+))?)", RegexOptions.CultureInvariant);

    private static readonly Regex ConfigVersion = new(@"^libcurl ([.0-9]+((-DEV)|(-rc[0-9])|(-[0-9]+))?)$", RegexOptions.CultureInvariant);

    private static readonly Regex ConfigVernum = new("^(..)(..)(..)$", RegexOptions.CultureInvariant);

    private static readonly Regex Suffix = new(@"-\w*$", RegexOptions.CultureInvariant);

    /// <summary>Runs a <c>%PERL</c> line when its program is <c>test1022.pl</c>.</summary>
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
            ? new(3, $"Usage: {words[0]} curl-config-script curl-version-output-file version|vernum\n")
            : Compare(words[2], words[3]);
        return UpstreamCurlConfigScriptLine.Redirect(result, redirect);
    }

    private static UpstreamPerlOneLinerResult Compare(string versionFile, string what)
    {
        if (!File.Exists(versionFile))
        {
            return new(NoSuchFile, "");
        }

        bool isVersion = what == "version";
        string version = CurlsVersion(versionFile, isVersion);
        string configVersion = CurlConfigsVersion(UpstreamCurlConfig.Lines($"--{what}").FirstOrDefault(""), isVersion);
        return version.Length > 0 && version == configVersion
            ? new(0, "")
            : new(1, $"Mismatch in --version:\ncurl:        {version}\ncurl-config: {configVersion}\n");
    }

    // The libcurl version on curl --version's first line; for vernum without a -DEV or -rc suffix.
    private static string CurlsVersion(string versionFile, bool isVersion)
    {
        Match curl = CurlVersion.Match(File.ReadLines(versionFile).FirstOrDefault(""));
        string version = curl.Success ? curl.Groups[1].Value : "";
        return isVersion ? version : Suffix.Replace(version, "");
    }

    private static string CurlConfigsVersion(string line, bool isVersion)
    {
        Match config = (isVersion ? ConfigVersion : ConfigVernum).Match(line);
        return !config.Success ? IllegalValue : isVersion ? config.Groups[1].Value : Decimal(config);
    }

    private static string Decimal(Match vernum) =>
        string.Join('.', vernum.Groups.Values.Skip(1).Select(group => Hex(group.Value)));

    // Curl's curl-config --vernum is always six hexadecimal digits, so Perl's hex() reads all of each pair.
    private static string Hex(string digits) =>
        int.Parse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
}
