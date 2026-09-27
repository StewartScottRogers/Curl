using System.Text;

namespace Curl.Cli;

/// <summary>
/// The lines <c>-M</c> / <c>--manual</c> prints on standard output: curl 8.21.0's built-in manual, as the
/// mingw reference build printed it (7849 lines, measured 2026-09-27), kept in the embedded resource
/// <c>CurlManual.txt</c>. It does not depend on the terminal width. The lines carry no line terminator;
/// the console layer chooses the newline.
/// </summary>
public static class CurlManual
{
    private const string ResourceName = "Curl.Cli.CurlManual.txt";

    /// <summary>Reads the manual's lines from the embedded resource, each without its terminator.</summary>
    /// <returns>The lines, the last one empty as curl's manual ends with a blank line.</returns>
    public static IReadOnlyList<string> Lines()
    {
        using Stream stream = typeof(CurlManual).Assembly.GetManifestResourceStream(ResourceName)!;
        using StreamReader reader = new(stream, Encoding.ASCII);
        List<string> lines = [];
        while (reader.ReadLine() is string line)
        {
            lines.Add(line);
        }

        return lines;
    }
}
