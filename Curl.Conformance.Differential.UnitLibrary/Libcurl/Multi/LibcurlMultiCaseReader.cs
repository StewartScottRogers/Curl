using System.Text;
using System.Text.Json;

namespace Curl.Conformance.Differential.Libcurl.Multi;

/// <summary>
/// Reads the tester's multi-mode files (<c>m_in.json</c> and its kin: a JSON array of cases,
/// each an array of steps) and share-mode file (<c>sh.json</c>: one array of steps). Each step
/// is an array whose first item is the operation. Uses <see cref="JsonDocument"/>, never a
/// reflection serialiser (AOT).
/// </summary>
public static class LibcurlMultiCaseReader
{
    private const string EmbeddedCasePrefix = "Curl.Conformance.Differential.Cases.";

    /// <summary>Reads the text of a case file embedded from this library's <c>Cases</c> folder.</summary>
    /// <param name="casePath">The file's path under <c>Cases</c>, such as <c>LibcurlMulti/m_in.json</c>.</param>
    /// <returns>The file's text.</returns>
    /// <exception cref="FileNotFoundException">No file is embedded at <paramref name="casePath"/>.</exception>
    public static string ReadEmbedded(string casePath)
    {
        ArgumentNullException.ThrowIfNull(casePath);
        using var stream = typeof(LibcurlMultiCaseReader).Assembly.GetManifestResourceStream(EmbeddedCasePrefix + casePath.Replace('/', '.'))
            ?? throw new FileNotFoundException($"No case file \"{casePath}\" is embedded.", casePath);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Reads every case in a multi-mode file.</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>Each case's steps, in file order.</returns>
    public static IReadOnlyList<IReadOnlyList<LibcurlMultiStep>> ReadMulti(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(ReadSteps)];
    }

    /// <summary>Reads the one case in a share-mode file.</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The case's steps, in file order.</returns>
    public static IReadOnlyList<LibcurlMultiStep> ReadShare(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        return ReadSteps(document.RootElement);
    }

    private static IReadOnlyList<LibcurlMultiStep> ReadSteps(JsonElement steps) => [.. steps.EnumerateArray().Select(ReadStep)];

    /// <summary>Reads the steps whose shape is not <c>[operation]</c> or <c>[operation, number]</c>.</summary>
    private static readonly Dictionary<string, Func<JsonElement, LibcurlMultiStep>> ShapedSteps = new(StringComparer.Ordinal)
    {
        ["easy"] = step => new("easy", [.. step[1].EnumerateArray().Select(ReadOption)], 0, null),
        ["setopt"] = step => new("setopt", [], 0, ReadValue(step[1].GetInt32(), step[2].GetString()!, step[3])),
        ["sset"] = step => new("sset", [], 0, ReadValue(step[1].GetInt32(), KindOf(step[2]), step[2])),
        ["ssetnull"] = step => new("ssetnull", [], 0, new(step[1].GetInt32(), "null", 0, null)),
    };

    private static LibcurlMultiStep ReadStep(JsonElement step)
    {
        var operation = step[0].GetString()!;
        return ShapedSteps.TryGetValue(operation, out var read)
            ? read(step)
            : new(operation, [], step.GetArrayLength() > 1 ? step[1].GetInt64() : 0, null);
    }

    private static LibcurlMultiValue ReadOption(JsonElement option) => ReadValue(option[0].GetInt32(), option[1].GetString()!, option[2]);

    private static string KindOf(JsonElement value) => value.ValueKind == JsonValueKind.String ? "s" : "l";

    private static LibcurlMultiValue ReadValue(int id, string kind, JsonElement value) =>
        new(id, kind, value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0, value.ValueKind == JsonValueKind.String ? value.GetString() : null);
}
