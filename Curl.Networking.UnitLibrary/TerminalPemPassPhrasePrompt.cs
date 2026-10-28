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

    /// <summary>
    /// The prompt on the process's standard error, reading standard input: unechoed key by key
    /// from a console, a line from anything redirected.
    /// </summary>
    public static TerminalPemPassPhrasePrompt Instance { get; } = new(Console.Error, ReadConsoleLine);

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

    [ExcludeFromCodeCoverage(Justification = "ADR-0083: reads the process's own console, which the tests replace through the constructor.")]
    private static string? ReadConsoleLine()
    {
        if (Console.IsInputRedirected)
        {
            return Console.In.ReadLine();
        }

        var answer = new StringBuilder();
        for (var key = Console.ReadKey(intercept: true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(intercept: true))
        {
            answer.Append(key.KeyChar);
        }

        return answer.ToString();
    }
}
