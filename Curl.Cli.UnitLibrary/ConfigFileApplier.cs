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
            return CommandLineRefusal.ConfigFileUnreadable(spelledOption, path, options.ErrorsHidden);
        }

        options.OpenConfigFileCount++;
        CommandLineRefusal? refusal = ApplyLines(options, shownName, contents, spelledOption, pathExists, dataFileReader);
        options.OpenConfigFileCount--;
        return refusal;
    }

    private static CommandLineRefusal? ApplyLines(CommandLineOptions options, string shownName, byte[] contents, string spelledOption, Func<string, bool> pathExists, IDataFileReader dataFileReader)
    {
        foreach (ConfigFileLine line in ConfigFileSyntax.ReadLines(shownName, contents))
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
