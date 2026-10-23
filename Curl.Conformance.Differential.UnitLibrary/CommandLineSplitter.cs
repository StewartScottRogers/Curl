using System.Text;

namespace Curl.Conformance.Differential;

/// <summary>
/// Splits a case's command line into arguments the way a POSIX shell does (Python's
/// <c>shlex.split</c>, which the tester's harness used): blanks separate arguments, single quotes
/// keep everything, double quotes keep everything but a backslash before <c>"</c> or <c>\</c>, and a
/// backslash outside quotes keeps the next character.
/// </summary>
public static class CommandLineSplitter
{
    /// <summary>Splits <paramref name="commandLine"/> into its arguments.</summary>
    /// <param name="commandLine">The command line, without the program name.</param>
    /// <returns>The arguments, in order.</returns>
    /// <exception cref="FormatException">A quote is not closed, or the line ends in a backslash.</exception>
    public static IReadOnlyList<string> Split(string commandLine)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        var inArgument = false;
        var index = 0;
        while (index < commandLine.Length)
        {
            var character = commandLine[index];
            if (Blanks.Contains(character, StringComparison.Ordinal))
            {
                AddArgument(arguments, current, inArgument);
                inArgument = false;
                index++;
                continue;
            }

            inArgument = true;
            index = ReadPart(commandLine, index, current);
        }

        AddArgument(arguments, current, inArgument);
        return arguments;
    }

    private const string Blanks = " \t\r\n";

    private static int ReadPart(string commandLine, int index, StringBuilder current) => commandLine[index] switch
    {
        '\'' => ReadSingleQuoted(commandLine, index + 1, current),
        '"' => ReadDoubleQuoted(commandLine, index + 1, current),
        '\\' => ReadEscaped(commandLine, index + 1, current),
        var character => AppendCharacter(current, character, index + 1),
    };

    private static bool EscapesInDoubleQuotes(string commandLine, int index) =>
        commandLine[index] == '\\' && index + 1 < commandLine.Length && "\"\\".Contains(commandLine[index + 1], StringComparison.Ordinal);

    private static void AddArgument(List<string> arguments, StringBuilder current, bool inArgument)
    {
        if (inArgument)
        {
            arguments.Add(current.ToString());
            current.Clear();
        }
    }

    private static int AppendCharacter(StringBuilder current, char character, int next)
    {
        current.Append(character);
        return next;
    }

    private static int ReadSingleQuoted(string commandLine, int start, StringBuilder current)
    {
        var close = commandLine.IndexOf('\'', start);
        if (close < 0)
        {
            throw new FormatException("No closing quotation.");
        }

        current.Append(commandLine, start, close - start);
        return close + 1;
    }

    private static int ReadDoubleQuoted(string commandLine, int start, StringBuilder current)
    {
        var index = start;
        while (index < commandLine.Length)
        {
            if (commandLine[index] == '"')
            {
                return index + 1;
            }

            index += EscapesInDoubleQuotes(commandLine, index) ? 1 : 0;
            current.Append(commandLine[index]);
            index++;
        }

        throw new FormatException("No closing quotation.");
    }

    private static int ReadEscaped(string commandLine, int start, StringBuilder current)
    {
        if (start >= commandLine.Length)
        {
            throw new FormatException("No escaped character.");
        }

        current.Append(commandLine[start]);
        return start + 1;
    }
}
