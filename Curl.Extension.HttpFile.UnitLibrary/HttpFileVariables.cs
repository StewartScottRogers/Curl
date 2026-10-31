using System.Text;
using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.HttpFile;

/// <summary>
/// The variables a .http file's requests use (ADR-1073): an environment's, then the file's own <c>@name = value</c>
/// lines, a later definition replacing an earlier one; <see cref="Substitute" /> replaces each <c>{{name}}</c> and
/// <c>{{$processEnv NAME}}</c> in a text.
/// </summary>
/// <param name="readEnvironmentVariable">Gives a process environment variable, or <see langword="null" /> when it is not set.</param>
internal sealed class HttpFileVariables(Func<string, string?> readEnvironmentVariable)
{
    private const string Open = "{{";

    private const string Close = "}}";

    private const string ProcessEnvironment = "$processEnv ";

    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);

    /// <summary>Sets a variable to a value taken as it is, as an environment's is.</summary>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">Its value.</param>
    public void Set(string name, string value) => values[name] = value;

    /// <summary>Sets a variable to a value whose own references are substituted first, as a file variable's are.</summary>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">Its value, which may reference variables defined before it.</param>
    /// <exception cref="ArgumentExpansionException">The value references no variable (exit 2).</exception>
    public void Define(string name, string value) => values[name] = Substitute(value);

    /// <summary>Gives <paramref name="text" /> with each <c>{{reference}}</c> replaced by its value; a <c>{{</c> with no <c>}}</c> after it stays.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The substituted text.</returns>
    /// <exception cref="ArgumentExpansionException">A reference names no variable (exit 2).</exception>
    public string Substitute(string text)
    {
        StringBuilder substituted = new(text.Length);
        int start = 0;
        (int open, int close) = NextReference(text, start);
        while (close >= 0)
        {
            substituted.Append(text, start, open - start).Append(ValueOf(text[(open + Open.Length)..close].Trim()));
            start = close + Close.Length;
            (open, close) = NextReference(text, start);
        }

        return substituted.Append(text, start, text.Length - start).ToString();
    }

    /// <summary>Gives where the next <c>{{</c> and the <c>}}</c> closing it are, or a close of -1 when there is none.</summary>
    private static (int Open, int Close) NextReference(string text, int start)
    {
        int open = text.IndexOf(Open, start, StringComparison.Ordinal);
        return open < 0 ? (open, -1) : (open, text.IndexOf(Close, open + Open.Length, StringComparison.Ordinal));
    }

    private string ValueOf(string reference)
    {
        if (reference.StartsWith(ProcessEnvironment, StringComparison.Ordinal))
        {
            return readEnvironmentVariable(reference[ProcessEnvironment.Length..].Trim()) ?? string.Empty;
        }

        return values.TryGetValue(reference, out string? value)
            ? value
            : throw new ArgumentExpansionException($"http-file: variable '{reference}' is not defined", CurlExitCode.FailedInit);
    }
}
