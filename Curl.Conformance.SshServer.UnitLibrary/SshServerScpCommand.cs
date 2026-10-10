using System.Text;

namespace Curl.Conformance.SshServer;

/// <summary>
/// An <c>scp</c> command line a client runs with <c>exec</c>, such as libssh2's
/// <c>scp -pf '/dir/file'</c> (send the file) or <c>scp -t '/dir/file'</c> (receive one), read
/// as the shell OpenSSH's <c>sshd</c> runs it would read it: options, then one path argument
/// unquoted from single quotes, double quotes and backslashes.
/// </summary>
/// <param name="IsSource">Whether the command is <c>-f</c>, the server sending the file; otherwise <c>-t</c>, the server receiving one.</param>
/// <param name="Path">The path argument, unquoted.</param>
internal sealed record SshServerScpCommand(bool IsSource, string Path)
{
    /// <summary>
    /// Reads an <c>exec</c> command as an <c>scp</c> command line.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>The command, or <see langword="null"/> when it is not <c>scp</c> with <c>-f</c> or <c>-t</c> and a path.</returns>
    internal static SshServerScpCommand? Parse(string command)
    {
        List<string> words = Words(command);
        if (words.Count < 3 || words[0] != "scp")
        {
            return null;
        }

        string options = string.Concat(words.Skip(1).SkipLast(1).Where(word => word.StartsWith('-')));
        bool source = options.Contains('f', StringComparison.Ordinal);
        return source || options.Contains('t', StringComparison.Ordinal) ? new(source, words[^1]) : null;
    }

    /// <summary>
    /// Splits a command line into words as a Bourne shell does: blanks separate words outside
    /// quotes, single quotes keep every byte, double quotes keep every byte but a backslash
    /// before <c>"</c>, <c>\</c>, <c>$</c> or a backtick, and a backslash outside quotes keeps the
    /// next byte.
    /// </summary>
    /// <param name="command">The command line.</param>
    /// <returns>The words.</returns>
    internal static List<string> Words(string command)
    {
        List<string> words = [];
        StringBuilder word = new();
        bool inWord = false;
        for (int index = 0; index < command.Length; index++)
        {
            char next = command[index];
            if (next == ' ')
            {
                EndWord(words, word, ref inWord);
                continue;
            }

            inWord = true;
            index = next switch
            {
                '\'' => AppendUntil(command, index + 1, '\'', word),
                '"' => AppendDoubleQuoted(command, index + 1, word),
                '\\' => AppendEscaped(command, index + 1, word),
                _ => Append(next, index, word),
            };
        }

        EndWord(words, word, ref inWord);
        return words;
    }

    private static void EndWord(List<string> words, StringBuilder word, ref bool inWord)
    {
        if (inWord)
        {
            words.Add(word.ToString());
            word.Clear();
            inWord = false;
        }
    }

    private static int Append(char character, int index, StringBuilder word)
    {
        word.Append(character);
        return index;
    }

    // A backslash at the very end of the line keeps itself.
    private static int AppendEscaped(string command, int index, StringBuilder word) =>
        index < command.Length ? Append(command[index], index, word) : Append('\\', index - 1, word);

    // Returns the index of the closing quote, or the end of the line when there is none.
    private static int AppendUntil(string command, int index, char closing, StringBuilder word)
    {
        int end = command.IndexOf(closing, index);
        end = end < 0 ? command.Length : end;
        word.Append(command, index, end - index);
        return end;
    }

    private static int AppendDoubleQuoted(string command, int index, StringBuilder word)
    {
        while (index < command.Length && command[index] != '"')
        {
            index += EscapesInDoubleQuotes(command, index) ? 1 : 0;
            word.Append(command[index]);
            index++;
        }

        return index;
    }

    // Inside double quotes a backslash escapes only ", \, $ and a backtick.
    private static bool EscapesInDoubleQuotes(string command, int index) =>
        command[index] == '\\' && index + 1 < command.Length && "\"\\$`".Contains(command[index + 1], StringComparison.Ordinal);
}
