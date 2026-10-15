using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Splits a test case's <c>&lt;client&gt;&lt;command&gt;</c> into arguments the way the POSIX shell
/// upstream's <c>runtests.pl</c> hands it to does: blanks and line breaks separate words, single
/// quotes keep every character, double quotes keep every character but let a backslash escape
/// <c>"</c>, <c>\</c>, <c>$</c> and <c>`</c>, and an unquoted backslash keeps the next character.
/// </summary>
/// <remarks>
/// A <c>&gt; file</c> or <c>&gt;&gt; file</c> that starts a word is read as the command's one
/// redirect of standard output, into <see cref="UpstreamCommandLine.StandardOutputFile"/> (test321).
/// Other shell syntax that would do more than quote - an unquoted <c>|</c>, <c>;</c>, <c>&amp;</c>,
/// <c>&lt;</c>, a second or mid-word <c>&gt;</c>, <c>$</c> or <c>`</c>, or an unescaped <c>$</c> or <c>`</c> inside
/// double quotes - is not carried out; it is reported in
/// <see cref="UpstreamCommandLine.UnsupportedShellSyntax"/> so the case can be skipped with a
/// reason. An unclosed quote runs to the end of the command. Each argument's bytes, carried as
/// Latin-1, are decoded as UTF-8, the encoding the test files are written in.
/// </remarks>
internal static class UpstreamCommandLineSplitter
{
    private const string UnsupportedUnquoted = "|;&<>$`";

    private const string EscapableInDoubleQuotes = "\"\\$`";

    private const string ExpandedInDoubleQuotes = "$`";

    /// <summary>Splits one command.</summary>
    /// <param name="command">The command, after the test file's expansion, one character per byte.</param>
    /// <returns>The arguments and the first shell syntax that was not carried out.</returns>
    public static UpstreamCommandLine Split(string command)
    {
        WordReader reader = new(command);
        return reader.Read();
    }

    private sealed class WordReader(string command)
    {
        private readonly List<string> arguments = [];
        private readonly StringBuilder word = new();
        private bool inWord;
        private char? unsupported;
        private int index;
        private int redirects;
        private bool readsRedirectTarget;
        private string? standardOutputFile;
        private bool appends;

        public UpstreamCommandLine Read()
        {
            while (index < command.Length)
            {
                ReadCharacter(command[index++]);
            }

            EndWord();
            // A redirect with no file after it is a shell syntax error, not carried out.
            unsupported ??= readsRedirectTarget ? '>' : null;
            return new UpstreamCommandLine(arguments, unsupported) { StandardOutputFile = standardOutputFile, AppendsStandardOutput = appends };
        }

        private void ReadCharacter(char character)
        {
            if (char.IsWhiteSpace(character))
            {
                EndWord();
                return;
            }

            if (character == '>' && StartsRedirect())
            {
                return;
            }

            inWord = true;
            unsupported ??= UnsupportedUnquoted.Contains(character, StringComparison.Ordinal) ? character : null;
            ReadWordCharacter(character);
        }

        private void ReadWordCharacter(char character)
        {
            switch (character)
            {
                case '\'':
                    ReadSingleQuoted();
                    break;
                case '"':
                    ReadDoubleQuoted();
                    break;
                case '\\':
                    AppendNext();
                    break;
                default:
                    word.Append(character);
                    break;
            }
        }

        private void ReadSingleQuoted()
        {
            while (index < command.Length && command[index] != '\'')
            {
                word.Append(command[index++]);
            }

            index++;
        }

        private void ReadDoubleQuoted()
        {
            while (index < command.Length && command[index] != '"')
            {
                ReadDoubleQuotedCharacter();
            }

            index++;
        }

        private void ReadDoubleQuotedCharacter()
        {
            bool escapes = EscapesInDoubleQuotes();
            unsupported ??= !escapes && ExpandedInDoubleQuotes.Contains(command[index], StringComparison.Ordinal) ? command[index] : null;
            index += escapes ? 1 : 0;
            word.Append(command[index++]);
        }

        // Whether the character at index is a backslash that escapes the next one inside double quotes.
        private bool EscapesInDoubleQuotes() =>
            command[index] == '\\' && index + 1 < command.Length && EscapableInDoubleQuotes.Contains(command[index + 1], StringComparison.Ordinal);

        private void AppendNext()
        {
            if (index < command.Length)
            {
                word.Append(command[index++]);
            }
        }

        // A > or >> that starts a word, the first in the command, redirects standard output to the
        // next word; one inside a word (2>, a file descriptor) or a second one is not carried out.
        private bool StartsRedirect()
        {
            if (inWord || readsRedirectTarget || redirects++ > 0)
            {
                return false;
            }

            appends = index < command.Length && command[index] == '>';
            index += appends ? 1 : 0;
            readsRedirectTarget = true;
            return true;
        }

        private void EndWord()
        {
            if (inWord)
            {
                string value = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(word.ToString()));
                standardOutputFile = readsRedirectTarget ? value : standardOutputFile;
                arguments.AddRange(readsRedirectTarget ? [] : [value]);
                readsRedirectTarget = false;
            }

            word.Clear();
            inWord = false;
        }
    }
}
