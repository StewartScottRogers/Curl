using System.Text.Json;

namespace Curl.Conformance.Differential.Libcurl.Scenarios;

/// <summary>
/// Reads the tester's <c>scen.json</c>, an array of arrays whose first element is the kind letter,
/// into <see cref="LibcurlScenario"/>s with <see cref="JsonDocument"/> (AOT-safe, ADR-0791).
/// </summary>
public static class LibcurlScenarioFileReader
{
    /// <summary>Reads every scenario in <paramref name="json"/>, numbered from 0 in file order.</summary>
    /// <exception cref="FormatException">An entry has a kind letter the tester never wrote.</exception>
    public static IReadOnlyList<LibcurlScenario> Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var scenarios = new List<LibcurlScenario>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            scenarios.Add(ReadScenario(scenarios.Count, entry));
        }

        return scenarios;
    }

    /// <summary>Reads the inputs of each kind from a scenario's array, given its index.</summary>
    private static readonly Dictionary<string, Func<int, JsonElement, LibcurlScenario>> KindReaders = new(StringComparer.Ordinal)
    {
        ["U"] = (index, entry) => new(index, "U", entry[1].GetString(), 0, entry[2].GetInt32(), null, []),
        ["P"] = (index, entry) => new(index, "P", entry[1].GetString(), entry[2].GetInt32(), entry[3].GetInt32(), entry[4].GetString(), []),
        ["E"] = (index, entry) => new(index, "E", null, 0, 0, null, Convert.FromHexString(entry[1].GetString()!)),
        ["N"] = (index, entry) => new(index, "N", null, 0, 0, null, Convert.FromHexString(entry[1].GetString()!)),
        ["D"] = (index, entry) => new(index, "D", entry[1].GetString(), 0, 0, null, []),
        ["S"] = (index, _) => new(index, "S", null, 0, 0, null, []),
        ["V"] = (index, _) => new(index, "V", null, 0, 0, null, []),
    };

    private static LibcurlScenario ReadScenario(int index, JsonElement entry)
    {
        var kind = entry[0].GetString() ?? string.Empty;
        return KindReaders.TryGetValue(kind, out var read)
            ? read(index, entry)
            : throw new FormatException($"Scenario {index} has the unknown kind '{kind}'.");
    }
}
