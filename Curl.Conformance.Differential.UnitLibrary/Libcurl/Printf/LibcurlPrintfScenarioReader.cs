using System.Text;
using System.Text.Json;

namespace Curl.Conformance.Differential.Libcurl.Printf;

/// <summary>
/// Reads the tester's printf input files (<c>t1.json</c>, <c>pf_in.json</c>): a JSON array of
/// scenarios, each <c>[format, [[kind, value], ...]]</c>. Uses <see cref="JsonDocument"/>, never
/// a reflection serialiser (AOT).
/// </summary>
public static class LibcurlPrintfScenarioReader
{
    private const string EmbeddedCasePrefix = "Curl.Conformance.Differential.Cases.";

    private static readonly Dictionary<string, Func<JsonElement, LibcurlPrintfArgument>> ArgumentReaders = new(StringComparer.Ordinal)
    {
        ["s"] = value => new('s', null, value.ValueKind == JsonValueKind.Null ? null : value.GetString()),
        ["d"] = value => new('d', Convert.ToInt64(value.GetString(), 16), null),
        ["u"] = value => new('u', unchecked((long)value.GetUInt64()), null),
        ["i"] = value => ReadInteger('i', value),
        ["l"] = value => ReadInteger('l', value),
        ["c"] = value => ReadInteger('c', value),
        ["q"] = value => ReadInteger('q', value),
        ["p"] = value => ReadInteger('p', value),
    };

    /// <summary>Reads the scenarios of an input file embedded from this library's <c>Cases</c> folder.</summary>
    /// <param name="casePath">The file's path under <c>Cases</c>, such as <c>LibcurlPrintf/PrintfScenarios.json</c>.</param>
    /// <returns>The scenarios, in file order.</returns>
    /// <exception cref="FileNotFoundException">No file is embedded at <paramref name="casePath"/>.</exception>
    public static IReadOnlyList<LibcurlPrintfScenario> ReadEmbedded(string casePath)
    {
        ArgumentNullException.ThrowIfNull(casePath);
        using var stream = typeof(LibcurlPrintfScenarioReader).Assembly.GetManifestResourceStream(EmbeddedCasePrefix + casePath.Replace('/', '.'))
            ?? throw new FileNotFoundException($"No printf input file \"{casePath}\" is embedded.", casePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Read(reader.ReadToEnd());
    }

    /// <summary>Reads every scenario in <paramref name="json"/>.</summary>
    /// <param name="json">The input file's text.</param>
    /// <returns>The scenarios, in file order.</returns>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    /// <exception cref="FormatException">An argument's kind letter is not one the tester used, or a scenario has more than <see cref="LibcurlPrintfScenario.MaximumArguments"/> arguments.</exception>
    public static IReadOnlyList<LibcurlPrintfScenario> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(ReadScenario)];
    }

    private static LibcurlPrintfScenario ReadScenario(JsonElement scenario, int index)
    {
        var arguments = scenario[1].EnumerateArray().Select(ReadArgument).ToList();
        if (arguments.Count > LibcurlPrintfScenario.MaximumArguments)
        {
            throw new FormatException($"Scenario {index} has {arguments.Count} arguments; at most {LibcurlPrintfScenario.MaximumArguments} are passed.");
        }

        return new LibcurlPrintfScenario(index, scenario[0].GetString()!, arguments);
    }

    private static LibcurlPrintfArgument ReadArgument(JsonElement argument)
    {
        var kind = argument[0].GetString()!;
        return ArgumentReaders.TryGetValue(kind, out var read)
            ? read(argument[1])
            : throw new FormatException($"\"{kind}\" is not a printf argument kind the tester used.");
    }

    private static LibcurlPrintfArgument ReadInteger(char kind, JsonElement value) =>
        new(kind, value.ValueKind == JsonValueKind.Null ? null : value.GetInt64(), null);
}
