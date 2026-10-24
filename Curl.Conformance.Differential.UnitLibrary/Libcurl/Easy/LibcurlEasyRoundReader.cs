using System.Text;
using System.Text.Json;

namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>
/// Reads the tester's easy-mode round files (<c>e_in.json</c>, <c>h_round.json</c>): a JSON
/// array of rounds, each <c>{"opts": [[id, kind, value], ...], "info": [...], "hq": [...],
/// "hwalk": [...], "reset": bool}</c>, with <c>hq</c>, <c>hwalk</c> and <c>reset</c> optional.
/// Uses <see cref="JsonDocument"/>, never a reflection serialiser (AOT).
/// </summary>
public static class LibcurlEasyRoundReader
{
    private const string EmbeddedCasePrefix = "Curl.Conformance.Differential.Cases.";

    private static readonly Dictionary<string, LibcurlEasyOptionKind> Kinds = new(StringComparer.Ordinal)
    {
        ["l"] = LibcurlEasyOptionKind.Number,
        ["s"] = LibcurlEasyOptionKind.Text,
        ["L"] = LibcurlEasyOptionKind.List,
        ["W"] = LibcurlEasyOptionKind.WriteCallback,
        ["H"] = LibcurlEasyOptionKind.HeaderCallback,
    };

    /// <summary>Reads the rounds of a round file embedded from this library's <c>Cases</c> folder.</summary>
    /// <param name="casePath">The file's path under <c>Cases</c>, such as <c>LibcurlEasy/EasyRounds.json</c>.</param>
    /// <returns>The rounds, in file order.</returns>
    /// <exception cref="FileNotFoundException">No file is embedded at <paramref name="casePath"/>.</exception>
    public static IReadOnlyList<LibcurlEasyRound> ReadEmbedded(string casePath)
    {
        ArgumentNullException.ThrowIfNull(casePath);
        using var stream = typeof(LibcurlEasyRoundReader).Assembly.GetManifestResourceStream(EmbeddedCasePrefix + casePath.Replace('/', '.'))
            ?? throw new FileNotFoundException($"No round file \"{casePath}\" is embedded.", casePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Read(reader.ReadToEnd());
    }

    /// <summary>Reads every round in <paramref name="json"/>.</summary>
    /// <param name="json">The round file's text.</param>
    /// <returns>The rounds, in file order.</returns>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    /// <exception cref="KeyNotFoundException">A round has no <c>opts</c> or <c>info</c>.</exception>
    /// <exception cref="FormatException">An option's kind letter is not one the tester used.</exception>
    public static IReadOnlyList<LibcurlEasyRound> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(ReadRound)];
    }

    private static LibcurlEasyRound ReadRound(JsonElement round)
    {
        var options = new List<LibcurlEasyOption>();
        foreach (var option in round.GetProperty("opts").EnumerateArray())
        {
            options.Add(ReadOption(option));
        }

        var info = new List<int>();
        foreach (var id in round.GetProperty("info").EnumerateArray())
        {
            info.Add(id.GetInt32());
        }

        var resets = round.TryGetProperty("reset", out var reset) && reset.GetBoolean();
        return new LibcurlEasyRound(options, info, ReadQueries(round), ReadWalks(round), resets);
    }

    private static List<LibcurlEasyHeaderQuery> ReadQueries(JsonElement round)
    {
        var queries = new List<LibcurlEasyHeaderQuery>();
        if (round.TryGetProperty("hq", out var hq))
        {
            foreach (var q in hq.EnumerateArray())
            {
                queries.Add(new LibcurlEasyHeaderQuery(q[0].GetString(), q[1].GetInt32(), q[2].GetInt32(), q[3].GetInt32()));
            }
        }

        return queries;
    }

    private static List<LibcurlEasyHeaderWalk> ReadWalks(JsonElement round)
    {
        var walks = new List<LibcurlEasyHeaderWalk>();
        if (round.TryGetProperty("hwalk", out var hwalk))
        {
            foreach (var w in hwalk.EnumerateArray())
            {
                walks.Add(new LibcurlEasyHeaderWalk(w[0].GetInt32(), w[1].GetInt32()));
            }
        }

        return walks;
    }

    private static LibcurlEasyOption ReadOption(JsonElement option)
    {
        var id = option[0].GetInt32();
        var letter = option[1].GetString()!;
        if (!Kinds.TryGetValue(letter, out var kind))
        {
            throw new FormatException($"Option {id} has the unknown kind \"{letter}\".");
        }

        var value = option[2];
        var list = new List<string>();
        if (kind == LibcurlEasyOptionKind.List)
        {
            foreach (var text in value.EnumerateArray())
            {
                list.Add(text.GetString()!);
            }
        }

        return new LibcurlEasyOption(
            id,
            kind,
            kind == LibcurlEasyOptionKind.Number ? value.GetInt64() : 0,
            kind == LibcurlEasyOptionKind.Text ? value.GetString() : null,
            list);
    }
}
