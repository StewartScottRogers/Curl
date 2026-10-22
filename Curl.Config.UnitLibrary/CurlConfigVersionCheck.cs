using System.Globalization;

namespace Curl.Config;

/// <summary>
/// <c>curl-config --checkfor</c>, as curl 8.22.0's <c>curl-config.in</c> compares: the requested
/// version's major, minor and patch fields, cut at dots as <c>cut -d. -f1</c>, <c>-f2</c> and <c>-f3</c>
/// do (a value with no dot is its own every field) with the patch cut again at its first dash, against
/// the build's. A field that is not a number fails every <c>test</c> it is compared in, as <c>sh</c>'s
/// <c>test</c> does.
/// </summary>
internal static class CurlConfigVersionCheck
{
    /// <summary>Exits 0 when <paramref name="version"/> is at least <paramref name="requested"/>; otherwise says so and exits 1.</summary>
    /// <param name="requested">The version asked for; empty when <c>--checkfor</c> is the last argument.</param>
    /// <param name="version">The build's version, such as <c>8.22.0</c>.</param>
    /// <param name="output">Standard output.</param>
    /// <returns>0 or 1.</returns>
    public static int Run(string requested, string version, TextWriter output)
    {
        if (IsAtLeast(Fields(version), Fields(requested)))
        {
            return 0;
        }

        output.Write($"requested version {requested} is newer than existing {version}\n");
        return 1;
    }

    private static bool IsAtLeast(int?[] have, int?[] want) =>
        have[0] > want[0] || (have[0] == want[0] && (have[1] > want[1] || (have[1] == want[1] && want[2] <= have[2])));

    private static int?[] Fields(string version)
    {
        string[] parts = version.Split('.');
        return [Number(Field(parts, 0)), Number(Field(parts, 1)), Number(Field(parts, 2).Split('-')[0])];
    }

    private static string Field(string[] parts, int index) =>
        parts.Length == 1 ? parts[0] : index < parts.Length ? parts[index] : "";

    private static int? Number(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : null;
}
