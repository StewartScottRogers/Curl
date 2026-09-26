using System.Globalization;

namespace Curl.Console;

/// <summary>
/// Resolves the terminal width curl 8.21.0's <c>get_terminal_columns</c> gives the warning
/// wrapper (see <see cref="WarningLineWrapper" />).
/// </summary>
/// <remarks>
/// In curl's order: the <c>COLUMNS</c> environment variable when it reads as a whole number
/// from 21 to 9999; otherwise the width of the console standard error is attached to, when it
/// is one and that width is from 1 to 9999; otherwise <see cref="Default" />. On Windows curl
/// takes the console width as the window's <c>Right - Left</c>, one less than
/// <see cref="System.Console.WindowWidth" />, so an 80-column console gives 79.
/// </remarks>
internal static class TerminalColumns
{
    /// <summary>The width curl uses when neither <c>COLUMNS</c> nor a console gives one.</summary>
    internal const int Default = 79;

    /// <summary>The environment variable curl reads first.</summary>
    internal const string ColumnsVariableName = "COLUMNS";

    /// <summary>
    /// Resolves the width from this process's <c>COLUMNS</c> and standard-error console.
    /// </summary>
    /// <returns>The terminal width.</returns>
    internal static int Resolve() =>
        Resolve(Environment.GetEnvironmentVariable(ColumnsVariableName), ReadStandardErrorConsoleColumns);

    /// <summary>
    /// Resolves the width from <paramref name="columnsVariable" />, then
    /// <paramref name="readConsoleColumns" />, then <see cref="Default" />.
    /// </summary>
    /// <param name="columnsVariable">The <c>COLUMNS</c> value, or <see langword="null" /> when it is not set.</param>
    /// <param name="readConsoleColumns">Reads the standard-error console's width, or <see langword="null" /> when there is none.</param>
    /// <returns>The terminal width.</returns>
    internal static int Resolve(string? columnsVariable, Func<int?> readConsoleColumns)
    {
        if (TryParseColumnsVariable(columnsVariable, out int columns))
        {
            return columns;
        }

        return readConsoleColumns() is int consoleColumns and > 0 and < 10000 ? consoleColumns : Default;
    }

    /// <summary>
    /// Reads a <c>COLUMNS</c> value as curl's <c>strtol</c> check does: optional leading
    /// white space and sign, then only digits, from 21 to 9999.
    /// </summary>
    /// <param name="columnsVariable">The value, or <see langword="null" />.</param>
    /// <param name="columns">The width, when the value is used.</param>
    /// <returns>Whether curl uses the value.</returns>
    internal static bool TryParseColumnsVariable(string? columnsVariable, out int columns) =>
        int.TryParse(
            columnsVariable,
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out columns)
        && columns is > 20 and < 10000;

    /// <summary>
    /// Reads the width of the console standard error is attached to, in curl's terms.
    /// </summary>
    /// <returns>The width, or <see langword="null" /> when standard error is not a console.</returns>
    internal static int? ReadStandardErrorConsoleColumns() =>
        ReadStandardErrorConsoleColumns(System.Console.IsErrorRedirected, ReadConsoleWindowWidth, OperatingSystem.IsWindows());

    /// <summary>
    /// Reads the console width in curl's terms: one less than the window width on Windows,
    /// the window width elsewhere.
    /// </summary>
    /// <param name="isErrorRedirected">Whether standard error is anything but a console.</param>
    /// <param name="readWindowWidth">Reads the console window's width; may throw <see cref="IOException" />.</param>
    /// <param name="runsOnWindows">Whether the process runs on Windows.</param>
    /// <returns>The width, or <see langword="null" /> when standard error is redirected or the width cannot be read.</returns>
    internal static int? ReadStandardErrorConsoleColumns(bool isErrorRedirected, Func<int> readWindowWidth, bool runsOnWindows)
    {
        if (isErrorRedirected)
        {
            return null;
        }

        try
        {
            int windowWidth = readWindowWidth();

            return runsOnWindows ? windowWidth - 1 : windowWidth;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Reads <see cref="System.Console.WindowWidth" />.</summary>
    /// <returns>The console window's width in columns.</returns>
    /// <exception cref="IOException">No console is attached.</exception>
    internal static int ReadConsoleWindowWidth() => System.Console.WindowWidth;
}
