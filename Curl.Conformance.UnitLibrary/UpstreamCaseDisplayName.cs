using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Names an upstream case's data-driven test row as Test Explorer and CI show it -
/// <c>test798 [http]: HTTP cookies in a folded header</c> - from its number, the servers its
/// <c>&lt;client&gt;&lt;server&gt;</c> part lists and its <c>&lt;client&gt;&lt;name&gt;</c> line.
/// </summary>
public static class UpstreamCaseDisplayName
{
    /// <summary>The longest display name; a longer one is cut and ends in <c>...</c>.</summary>
    public const int MaximumLength = 80;

    private const string Ellipsis = "...";

    /// <summary>Names a case's row.</summary>
    /// <param name="testNumber">The case's number.</param>
    /// <param name="testCase">The case as written, or <see langword="null"/> when its file does not parse.</param>
    /// <returns><c>test&lt;N&gt; [&lt;servers&gt;]: &lt;name&gt;</c>, without the parts the case lacks, at most <see cref="MaximumLength"/> characters.</returns>
    public static string For(int testNumber, UpstreamTestCase? testCase)
    {
        StringBuilder name = new($"test{testNumber}");
        IReadOnlyList<string> servers = Servers(testCase);
        if (servers.Count > 0)
        {
            name.Append(" [").AppendJoin(", ", servers).Append(']');
        }

        string description = PartText(testCase, "name").Trim();
        if (description.Length > 0)
        {
            name.Append(": ").AppendJoin(' ', description.Split((char[])['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        }

        return name.Length > MaximumLength ? string.Concat(name.ToString(0, MaximumLength - Ellipsis.Length), Ellipsis) : name.ToString();
    }

    /// <summary>
    /// The servers a case needs, the first word of each line of its <c>&lt;client&gt;&lt;server&gt;</c>
    /// part (<c>https test-localhost.pem</c> is <c>https</c>), in order; its test row carries one
    /// test category per server.
    /// </summary>
    /// <param name="testCase">The case as written, or <see langword="null"/> when its file does not parse.</param>
    /// <returns>The server names; empty when the case names none.</returns>
    public static IReadOnlyList<string> Servers(UpstreamTestCase? testCase) =>
        PartText(testCase, "server")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', '\t')[0])
            .ToList();

    private static string PartText(UpstreamTestCase? testCase, string name) =>
        testCase?.Find("client", name) is { } part ? Encoding.Latin1.GetString(part.Content.Span) : string.Empty;
}
