using System.Text;
using System.Text.Json;

namespace Curl.Conformance.Differential;

/// <summary>
/// Reads a JSON case file with <see cref="JsonDocument"/> (no reflection serialiser, for AOT):
/// <c>{"cases": [{"name": ..., "command": ..., "stdin": ..., "files": {path: text},
/// "timeoutSeconds": 20, "compareStderr": true, "compareWire": true, "limitations": ["NoHttp2"]}]}</c>.
/// Only <c>name</c> and <c>command</c> are required; the rest default as shown, with no standard
/// input, no files and no limitations. Text is written as UTF-8.
/// </summary>
public static class DifferentialCaseFileReader
{
    private const int DefaultTimeoutSeconds = 20;

    /// <summary>Reads every case in <paramref name="json"/>.</summary>
    /// <param name="json">The case file's text.</param>
    /// <returns>The cases, in file order.</returns>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    /// <exception cref="KeyNotFoundException">A required property is missing.</exception>
    /// <exception cref="FormatException">A required property is <c>null</c>, or a command line does not split.</exception>
    public static IReadOnlyList<DifferentialCase> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.GetProperty("cases").EnumerateArray().Select(ReadCase)];
    }

    private static DifferentialCase ReadCase(JsonElement element)
    {
        var standardInput = element.TryGetProperty("stdin", out var stdin) ? Encoding.UTF8.GetBytes(RequiredString(stdin, "stdin")) : null;
        return new DifferentialCase(
            RequiredString(element.GetProperty("name"), "name"),
            CommandLineSplitter.Split(RequiredString(element.GetProperty("command"), "command")),
            standardInput,
            ReadFiles(element),
            TimeSpan.FromSeconds(element.TryGetProperty("timeoutSeconds", out var timeout) ? timeout.GetInt32() : DefaultTimeoutSeconds),
            !element.TryGetProperty("compareStderr", out var compareStderr) || compareStderr.GetBoolean(),
            !element.TryGetProperty("compareWire", out var compareWire) || compareWire.GetBoolean(),
            ReadLimitations(element));
    }

    private static string RequiredString(JsonElement value, string propertyName) =>
        value.GetString() ?? throw new FormatException($"The case property \"{propertyName}\" is null.");

    private static Dictionary<string, byte[]> ReadFiles(JsonElement element) =>
        element.TryGetProperty("files", out var files)
            ? files.EnumerateObject().ToDictionary(file => file.Name, file => Encoding.UTF8.GetBytes(RequiredString(file.Value, file.Name)), StringComparer.Ordinal)
            : new Dictionary<string, byte[]>(StringComparer.Ordinal);

    private static ReferenceBuildLimitation[] ReadLimitations(JsonElement element) =>
        element.TryGetProperty("limitations", out var limitations)
            ? [.. limitations.EnumerateArray().Select(limitation => Enum.Parse<ReferenceBuildLimitation>(RequiredString(limitation, "limitations")))]
            : [];
}
