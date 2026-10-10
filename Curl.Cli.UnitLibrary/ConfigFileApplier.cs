namespace Curl.Cli;

/// <summary>
/// Applies a <c>-K</c> / <c>--config</c> file as curl 8.21.0 does: reads it through the parse's
/// <see cref="IDataFileReader"/> (standard input for <c>-</c>), splits it with
/// <see cref="ConfigFileSyntax"/>, and applies each line, in order and in place of the option, as if
/// its option and parameter had been given on the command line. The first refused line stops it.
/// </summary>
/// <remarks>
/// Measured with the local curl 8.21.0 on 2026-09-26 (<c>curl -q -K &lt;file&gt;</c> with no
/// <c>.curlrc</c>): a line's parameter that its option does not use (<c>silent foo</c>,
/// <c>--output=x y</c>) is refused as <c>had unsupported trailing garbage</c>; <c>--output=x</c>
/// alone is accepted; an option not starting with <c>-</c> is a long name (<c>K file</c> is unknown,
/// <c>-K file</c> nests); a nested file is read at most <see cref="CommandLineRefusal.MaximumConfigFileDepth"/>
/// deep, counting the command line's; and every refusal is reported through
/// <see cref="CommandLineRefusal.ConfigFileOptionRefused"/> by each file it passes through.
/// </remarks>
internal static class ConfigFileApplier
{
    private const string StandardInputName = "-";

    /// <summary>Reads the file <paramref name="path"/> names and applies its lines to <paramref name="options"/>.</summary>
    /// <param name="options">The options being filled in.</param>
    /// <param name="path">The file name as given; <c>-</c> for standard input.</param>
    /// <param name="spelledOption">The <c>-K</c> option as typed, or as written in the enclosing file.</param>
    /// <param name="pathExists">The parse's path-existence check, handed on to each line.</param>
    /// <param name="dataFileReader">Reads the file, and every <c>@file</c> a line names.</param>
    /// <returns><see langword="null"/> when every line was applied; otherwise the refusal.</returns>
    internal static CommandLineRefusal? ApplyFile(CommandLineOptions options, string path, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        if (options.OpenConfigFileCount == CommandLineRefusal.MaximumConfigFileDepth)
        {
            return CommandLineRefusal.ConfigFileTooDeep(spelledOption, options.ErrorsHidden);
        }

        string shownName;
        byte[] contents;
        if (path == StandardInputName)
        {
            shownName = "<stdin>";
            contents = dataFileReader.ReadStandardInput();
        }
        else if (dataFileReader.TryReadFile(path, out contents))
        {
            shownName = path;
        }
        else
        {
            return CommandLineRefusal.ConfigFileUnreadable(spelledOption, path, options.ErrorsHidden, WrappedMessage.TerminalColumns(options.ReadEnvironmentVariable("COLUMNS")));
        }

        options.OpenConfigFileCount++;
        CommandLineRefusal? refusal = ApplyLines(options, shownName, contents, spelledOption, pathExists, dataFileReader);
        options.OpenConfigFileCount--;
        return refusal;
    }

    /// <summary>
    /// Reads the first of <paramref name="candidatePaths"/> that can be read as curl's default config
    /// file (<c>.curlrc</c>) and applies its lines to <paramref name="options"/>, as curl 8.21.0 does before
    /// reading the command line. A refused line stops the file, as in a <c>-K</c> file, but refuses
    /// nothing: its error lines (<c>curl: &lt;file&gt;:&lt;n&gt; config file option '&lt;option&gt;' &lt;reason&gt;</c>,
    /// hidden when an earlier line turned <c>-s</c> on) go to the warning lines and the command line is
    /// read as usual. The file does not count against <see cref="CommandLineRefusal.MaximumConfigFileDepth"/>.
    /// </summary>
    /// <param name="options">The options being filled in.</param>
    /// <param name="candidatePaths">The paths to try, in order, from <see cref="DefaultConfigFileSearch.CandidatePaths"/>.</param>
    /// <param name="pathExists">The parse's path-existence check, handed on to each line.</param>
    /// <param name="dataFileReader">Reads the file, and every file a line names.</param>
    internal static void ApplyDefaultFile(CommandLineOptions options, IReadOnlyList<string> candidatePaths, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        foreach (string path in candidatePaths)
        {
            if (dataFileReader.TryReadFile(path, out byte[] contents))
            {
                ApplyDefaultFileLines(options, path, contents, pathExists, dataFileReader);
                return;
            }
        }
    }

    private static void ApplyDefaultFileLines(CommandLineOptions options, string path, byte[] contents, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        options.ReadingConfigFileAsWireText = ReadsAsWireText(options, contents);
        foreach (ConfigFileLine line in ReadLines(options, path, contents))
        {
            options.AddWarningLinesUnlessSilent(line.WarningLines);
            CommandLineRefusal? lineRefusal = CommandLineParser.ApplyConfigFileLine(options, line.Option, line.Parameter, pathExists, dataFileReader);
            if (lineRefusal is not null)
            {
                options.AddErrorLines(CommandLineRefusal.ConfigFileLineErrorLines(path, line.Number, line.Option, lineRefusal, options.ErrorsHidden));
                options.ReadingConfigFileAsWireText = false;
                return;
            }
        }

        options.ReadingConfigFileAsWireText = false;
        options.DefaultConfigFile = path;
    }

    /// <summary>
    /// Whether a file's bytes are read in <see cref="CommandLineOptions.ConfigFileWireTextEncoding"/>
    /// rather than UTF-8: on Windows, when they are not valid UTF-8. curl 8.21.0's Windows build uses a
    /// config file's bytes raw, so a file written in the ANSI code page (<c>93 host:fake 94</c>) sends
    /// those bytes, with no leading-Unicode warning; reading the file in the code page the request side
    /// encodes in gives them back unchanged (BL-1973, upstream test470).
    /// </summary>
    private static bool ReadsAsWireText(CommandLineOptions options, byte[] contents) =>
        options.ConfigFileWireTextEncoding is not null && !System.Text.Unicode.Utf8.IsValid(contents);

    private static IReadOnlyList<ConfigFileLine> ReadLines(CommandLineOptions options, string shownName, byte[] contents) =>
        ConfigFileSyntax.ReadLines(shownName, contents, options.ReadingConfigFileAsWireText ? options.ConfigFileWireTextEncoding! : System.Text.Encoding.UTF8);

    private static CommandLineRefusal? ApplyLines(CommandLineOptions options, string shownName, byte[] contents, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        bool enclosingFileAsWireText = options.ReadingConfigFileAsWireText;
        options.ReadingConfigFileAsWireText = ReadsAsWireText(options, contents);
        CommandLineRefusal? refusal = ApplyEachLine(options, shownName, contents, spelledOption, pathExists, dataFileReader);
        options.ReadingConfigFileAsWireText = enclosingFileAsWireText;
        return refusal;
    }

    private static CommandLineRefusal? ApplyEachLine(CommandLineOptions options, string shownName, byte[] contents, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        foreach (ConfigFileLine line in ReadLines(options, shownName, contents))
        {
            options.AddWarningLinesUnlessSilent(line.WarningLines);
            CommandLineRefusal? lineRefusal = CommandLineParser.ApplyConfigFileLine(options, line.Option, line.Parameter, pathExists, dataFileReader);
            if (lineRefusal is not null)
            {
                return CommandLineRefusal.ConfigFileOptionRefused(spelledOption, shownName, line.Number, line.Option, lineRefusal, options.ErrorsHidden);
            }
        }

        return null;
    }
}
