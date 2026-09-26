using System.Text;

namespace Curl.Cli;

/// <summary>
/// The <see cref="IPasswordPrompt"/> backed by the console: writes the prompt to standard error and
/// reads keys without echoing them until Enter, as curl's Windows <c>getpass_r</c> does
/// (<see href="https://github.com/curl/curl/blob/master/src/tool_getpass.c"/>): a backspace removes
/// the last character typed, and a line terminator is written to standard error once the password
/// is read. When the console cannot be read (input redirected, no console), what has been typed so
/// far is the password.
/// </summary>
/// <param name="standardError">Where the prompt and the line terminator after the password are written.</param>
/// <param name="readKey">Reads one key without echoing it.</param>
public sealed class ConsolePasswordPrompt(TextWriter standardError, Func<ConsoleKeyInfo> readKey) : IPasswordPrompt
{
    /// <summary>The prompt on this process's console: standard error, and keys read with <see cref="Console.ReadKey(bool)"/> intercepting them.</summary>
    public static ConsolePasswordPrompt ForProcessConsole { get; } = new(Console.Error, () => Console.ReadKey(intercept: true));

    /// <inheritdoc/>
    public string ReadPassword(string prompt)
    {
        standardError.Write(prompt);
        standardError.Flush();
        string password = ReadKeysUntilEnter();
        standardError.WriteLine();
        return password;
    }

    private string ReadKeysUntilEnter()
    {
        StringBuilder typed = new();
        try
        {
            for (char key = readKey().KeyChar; key is not ('\r' or '\n'); key = readKey().KeyChar)
            {
                AppendOrErase(typed, key);
            }
        }
        catch (InvalidOperationException)
        {
            // The console cannot be read: keep what was typed.
        }

        return typed.ToString();
    }

    private static void AppendOrErase(StringBuilder typed, char key)
    {
        if (key != '\b')
        {
            typed.Append(key);
        }
        else if (typed.Length > 0)
        {
            typed.Length--;
        }
    }
}
