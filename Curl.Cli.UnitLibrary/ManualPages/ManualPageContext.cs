using System.Text;

namespace Curl.Cli.ManualPages;

/// <summary>The state one run of the manual page generator shares: its streams, files, settings and option index.</summary>
/// <param name="readFile">Reads a file's text by path, or returns <see langword="null"/> when there is none.</param>
/// <param name="date">The date the page is stamped with, <c>yyyy-MM-dd</c>.</param>
/// <param name="output">Standard output.</param>
/// <param name="error">Standard error.</param>
public sealed class ManualPageContext(Func<string, string?> readFile, string date, StringBuilder output, StringBuilder error)
{
    /// <summary>Standard output.</summary>
    public StringBuilder Output { get; } = output;

    /// <summary>Standard error.</summary>
    public StringBuilder Error { get; } = error;

    /// <summary>The date the page is stamped with.</summary>
    public string Date { get; } = date;

    /// <summary>The folder holding the option documents and <c>mainpage.idx</c> (<c>-d</c>).</summary>
    public string Directory { get; set; } = ".";

    /// <summary>The widest an ASCII line may be (<c>-c</c>).</summary>
    public int ColumnWidth { get; set; } = 79;

    /// <summary>The curl version the page names.</summary>
    public string Version { get; set; } = "unknown";

    /// <summary>The global options, listed for <c>`%GLOBALS`</c>.</summary>
    public string Globals { get; set; } = "";

    /// <summary>The count of quoted lines too long, the script's exit code.</summary>
    public int Errors { get; set; }

    /// <summary>The short and long names and help texts of every option document given.</summary>
    public ManualOptionIndex Index { get; } = new();

    /// <summary>The file's lines, or the script's <c>die</c> with <paramref name="dieMessage"/> when it is missing.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="dieMessage">What the script dies with.</param>
    /// <returns>The lines.</returns>
    public List<string> ReadLines(string path, string dieMessage)
    {
        if (readFile(path) is { } text)
        {
            return ManualPerlValues.Lines(text);
        }

        Error.Append(dieMessage).Append('\n');
        throw new ManualPageExit(2);
    }

    /// <summary>The path of a file in <see cref="Directory"/>, as the script joins them.</summary>
    /// <param name="fileName">The file's name.</param>
    /// <returns>The path.</returns>
    public string PathOf(string fileName) => $"{Directory}/{fileName}";
}
