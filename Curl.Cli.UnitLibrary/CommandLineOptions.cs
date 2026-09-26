namespace Curl.Cli;

/// <summary>
/// The settings a command line asks for, filled in by <see cref="CommandLineParser"/>
/// through the rows of <see cref="CommandLineOptionTable"/>. Each option in the table
/// sets one property here. This is only what was asked for: nothing here validates a URL,
/// touches the file system or starts a transfer.
/// </summary>
public sealed class CommandLineOptions
{
    private readonly List<string> urls = [];
    private readonly List<string> outputFiles = [];

    /// <summary>
    /// The URLs to transfer, in command-line order: positional arguments and
    /// <c>--url</c> values interleaved as they were given.
    /// </summary>
    public IReadOnlyList<string> Urls => urls;

    /// <summary><see langword="true"/> when <c>-s</c> / <c>--silent</c> was given.</summary>
    public bool Silent { get; internal set; }

    /// <summary><see langword="true"/> when <c>-S</c> / <c>--show-error</c> was given.</summary>
    public bool ShowError { get; internal set; }

    /// <summary>The <c>-o</c> / <c>--output</c> file names, in command-line order.</summary>
    public IReadOnlyList<string> OutputFiles => outputFiles;

    /// <summary>Appends <paramref name="url"/> to <see cref="Urls"/>, unchanged and unvalidated.</summary>
    /// <param name="url">A positional argument or a <c>--url</c> value.</param>
    internal void AddUrl(string url) => urls.Add(url);

    /// <summary>Appends <paramref name="outputFile"/> to <see cref="OutputFiles"/>; nothing is opened or created.</summary>
    /// <param name="outputFile">A <c>-o</c> / <c>--output</c> value.</param>
    internal void AddOutputFile(string outputFile) => outputFiles.Add(outputFile);
}
