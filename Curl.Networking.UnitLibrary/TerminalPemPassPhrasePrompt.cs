using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// The <see cref="IPemPassPhrasePrompt" /> curl 8.18.0's OpenSSL build shows (measured,
/// ADR-0948): <c>Enter PEM pass phrase:</c> on standard error, one line read, then a line
/// break, since the typed pass phrase is not echoed.
/// </summary>
/// <param name="promptOutput">Where the prompt and the line break after the answer go.</param>
/// <param name="readLine">Reads the answer, <see langword="null" /> at the end of the input.</param>
public sealed class TerminalPemPassPhrasePrompt(TextWriter promptOutput, Func<string?> readLine) : IPemPassPhrasePrompt
{
    /// <summary>OpenSSL's prompt text, written without a line break.</summary>
    public const string PromptText = "Enter PEM pass phrase:";

    /// <summary>Reads one key press, shown on the console unless <paramref name="intercept" /> is set.</summary>
    /// <param name="intercept"><see langword="true" /> to keep the key off the screen.</param>
    /// <returns>The key pressed.</returns>
    internal delegate ConsoleKeyInfo ReadConsoleKey(bool intercept);

    /// <summary>
    /// The prompt on the process's standard error, reading standard input: unechoed key by key
    /// from a console, a line from anything redirected.
    /// </summary>
    public static TerminalPemPassPhrasePrompt Instance { get; } = new(Console.Error, ReadProcessConsoleLine);

    /// <summary>
    /// Reads the answer: a line when the input is redirected, otherwise keys, each intercepted
    /// so none is echoed, up to Enter.
    /// </summary>
    /// <param name="inputRedirected">Whether standard input is redirected from the console.</param>
    /// <param name="redirectedInput">The line source used when the input is redirected.</param>
    /// <param name="readKey">The key source used when the input is the console.</param>
    internal static string? ReadConsoleLine(bool inputRedirected, TextReader redirectedInput, ReadConsoleKey readKey)
    {
        if (inputRedirected)
        {
            return redirectedInput.ReadLine();
        }

        var answer = new StringBuilder();
        for (var key = readKey(intercept: true); key.Key != ConsoleKey.Enter; key = readKey(intercept: true))
        {
            answer.Append(key.KeyChar);
        }

        return answer.ToString();
    }

    /// <inheritdoc />
    public string? AskPassPhrase()
    {
        promptOutput.Write(PromptText);
        promptOutput.Flush();
        var answer = readLine();
        promptOutput.Write('\n');
        promptOutput.Flush();
        return answer;
    }

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: reads the process's own console, which the tests replace through ReadConsoleLine's parameters.")]
    private static string? ReadProcessConsoleLine() => ReadConsoleLine(Console.IsInputRedirected, Console.In, Console.ReadKey);
}
