using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Scenarios;

/// <summary>
/// Replays one <see cref="LibcurlScenario"/> against an <see cref="ILibcurlScenarioTarget"/> and
/// writes what it returned as the tester's <c>LibTest</c> lines, each starting with the scenario's
/// index, with bytes as upper-case hexadecimal or <c>null</c> (ADR-0791).
/// </summary>
public static class LibcurlScenarioReplayer
{
    /// <summary>The <c>CURLU_*</c> flag sets a <c>U</c> scenario gets every part with.</summary>
    public static readonly IReadOnlyList<int> GetFlags = [0, 1, 2, 64, 4096, 8192, 16384, 1 | 64, 2 | 16384];

    /// <summary>The highest <c>CURLUPART_*</c> number, <c>CURLUPART_ZONEID</c>.</summary>
    private const int LastUrlPart = 10;

    /// <summary>
    /// Replays <paramref name="scenario"/>; an exception becomes the one line
    /// <c>{index} {kind} EXC {type}</c>, as <c>LibTest</c> wrote it.
    /// </summary>
    public static IReadOnlyList<string> Replay(LibcurlScenario scenario, ILibcurlScenarioTarget target)
    {
        try
        {
            return ReplayKind(scenario, target);
        }
        catch (Exception exception)
        {
            return [$"{scenario.Index} {scenario.Kind} EXC {exception.GetType().Name}"];
        }
    }

    /// <summary>Replays each kind; an unknown kind throws, which <see cref="Replay"/> writes as an <c>EXC</c> line.</summary>
    private static readonly Dictionary<string, Func<LibcurlScenario, ILibcurlScenarioTarget, IReadOnlyList<string>>> KindReplayers = new(StringComparer.Ordinal)
    {
        ["U"] = (scenario, target) => [ReplayUrl(scenario, target)],
        ["P"] = (scenario, target) => [ReplayPart(scenario, target)],
        ["E"] = (scenario, target) => [$"{scenario.Index} E {target.Escape(scenario.Bytes)}"],
        ["N"] = (scenario, target) => [ReplayUnescape(scenario, target)],
        ["D"] = (scenario, target) => [$"{scenario.Index} D {target.ParseDate(scenario.Text!)}"],
        ["S"] = (scenario, target) => ReplayErrorTexts(scenario.Index, target),
        ["V"] = (scenario, target) => [$"{scenario.Index} V {target.Version()}"],
    };

    private static IReadOnlyList<string> ReplayKind(LibcurlScenario scenario, ILibcurlScenarioTarget target) =>
        KindReplayers[scenario.Kind](scenario, target);

    private static string ReplayUrl(LibcurlScenario scenario, ILibcurlScenarioTarget target)
    {
        using var url = target.CreateUrl();
        var line = new StringBuilder($"{scenario.Index} U set={url.Set(0, scenario.Text, scenario.Flags)}");
        for (var part = 0; part <= LastUrlPart; part++)
        {
            foreach (var flags in GetFlags)
            {
                var code = url.Get(part, flags, out var value);
                line.Append($" |{part}/{flags}={code}:{Hex(value)}");
            }
        }

        return line.ToString();
    }

    private static string ReplayPart(LibcurlScenario scenario, ILibcurlScenarioTarget target)
    {
        using var url = target.CreateUrl();
        if (scenario.Text is { Length: > 0 } baseUrl)
        {
            url.Set(0, baseUrl, 0);
        }

        var set = url.Set(scenario.Part, scenario.Value, scenario.Flags);
        var urlCode = url.Get(0, 0, out var whole);
        var partCode = url.Get(scenario.Part, 0, out var part);
        return $"{scenario.Index} P set={set} url={urlCode}:{Hex(whole)} part={partCode}:{Hex(part)}";
    }

    private static string ReplayUnescape(LibcurlScenario scenario, ILibcurlScenarioTarget target) =>
        target.Unescape(scenario.Bytes) is { } bytes ? $"{scenario.Index} N {Hex(bytes)}" : $"{scenario.Index} N EXC";

    private static List<string> ReplayErrorTexts(int index, ILibcurlScenarioTarget target)
    {
        var lines = new List<string>();
        AddErrorTexts(lines, $"{index} easy", -2, 130, target.EasyErrorText);
        AddErrorTexts(lines, $"{index} multi", -3, 20, target.MultiErrorText);
        AddErrorTexts(lines, $"{index} share", -1, 12, target.ShareErrorText);
        AddErrorTexts(lines, $"{index} url", -1, 40, target.UrlErrorText);
        return lines;
    }

    private static void AddErrorTexts(List<string> lines, string prefix, int first, int last, Func<int, string> errorText)
    {
        for (var code = first; code <= last; code++)
        {
            lines.Add($"{prefix} {code} {errorText(code)}");
        }
    }

    private static string Hex(byte[]? bytes) => bytes is null ? "null" : Convert.ToHexString(bytes);
}
