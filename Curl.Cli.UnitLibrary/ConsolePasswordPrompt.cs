using System.Text;

namespace Curl.Cli;

/// <summary>
/// The <see cref="IPasswordPrompt"/> backed by the console, as curl 8.22.0's <c>getpass_r</c>
/// (<see href="https://github.com/curl/curl/blob/master/src/tool_getpass.c"/>): turns echo off,
/// writes the prompt to standard error and reads keys until Enter; a backspace removes the last
/// character typed. The line terminator after the password is written, and echo turned back on,
/// only when echo was turned off: on Windows always, and off Windows only when the input
/// <see cref="PosixTerminalPasswordInput"/> reads (<c>/dev/tty</c>, else standard input) is a
/// terminal (ADR-0775). When the input cannot be read or ends, what has been typed so far is the
/// password.
/// </summary>
/// <param name="standardError">Where the prompt and the line terminator after the password are written.</param>
/// <param name="readKey">Reads one key without echoing it.</param>
/// <param name="turnEchoOff">Turns echo off on the input, returning whether it did.</param>
/// <param name="turnEchoOn">Turns echo back on after <paramref name="turnEchoOff"/> turned it off.</param>
public sealed class ConsolePasswordPrompt(TextWriter standardError, Func<ConsoleKeyInfo> readKey, Func<bool> turnEchoOff, Action turnEchoOn) : IPasswordPrompt
{
    /// <summary>A prompt whose keys are always read without echo, as on the Windows console.</summary>
    /// <param name="standardError">Where the prompt and the line terminator after the password are written.</param>
    /// <param name="readKey">Reads one key without echoing it.</param>
    public ConsolePasswordPrompt(TextWriter standardError, Func<ConsoleKeyInfo> readKey)
        : this(standardError, readKey, EchoIsAlreadyOff, EchoNeedsNoRestoring)
    {
    }

    /// <summary>The prompt on this process's console: standard error, and keys read as <see cref="ForPlatform"/> chooses for this platform.</summary>
    public static ConsolePasswordPrompt ForProcessConsole { get; } = ForPlatform(OperatingSystem.IsWindows(), Console.Error);

    /// <inheritdoc/>
    public string ReadPassword(string prompt)
    {
        bool echoTurnedOff = turnEchoOff();
        standardError.Write(prompt);
        standardError.Flush();
        string password = ReadKeysUntilEnter();
        if (echoTurnedOff)
        {
            standardError.WriteLine();
            turnEchoOn();
        }

        return password;
    }

    /// <summary>
    /// On Windows, keys read with <see cref="Console.ReadKey(bool)"/> intercepting them; off Windows,
    /// <see cref="PosixTerminalPasswordInput"/>'s <c>/dev/tty</c> or standard input with its echo control.
    /// </summary>
    /// <param name="isWindows">Whether the prompt is for Windows.</param>
    /// <param name="standardError">Where the prompt and the line terminator after the password are written.</param>
    /// <returns>A new prompt; nothing is opened until it reads a key.</returns>
    public static ConsolePasswordPrompt ForPlatform(bool isWindows, TextWriter standardError) =>
        isWindows
            ? new(standardError, () => Console.ReadKey(intercept: true))
            : new(standardError, PosixTerminalPasswordInput.ReadKey, PosixTerminalPasswordInput.TurnEchoOff, PosixTerminalPasswordInput.TurnEchoOn);

    private static bool EchoIsAlreadyOff() => true;

    private static void EchoNeedsNoRestoring()
    {
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
            // The input cannot be read or has ended: keep what was typed.
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
