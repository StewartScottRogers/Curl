namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// The extended conformance cases known to fail, <c>Curl.Conformance.Extended.UnitTests/KnownFailures.txt</c>:
/// one <c>&lt;requirement id&gt; BL-####</c> per line, in any order, with <c>#</c> comments and blank
/// lines ignored. A listed case passes while it still fails, and fails once it passes so its line is
/// removed; a listed ID no test carries fails <see cref="FindProblems"/>.
/// </summary>
public sealed class KnownFailures
{
    private const string AssertionFailureTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestAssertException";

    private readonly Dictionary<string, string> taskIdsByRequirementId;
    private readonly List<string> formatProblems;

    private KnownFailures(Dictionary<string, string> taskIdsByRequirementId, List<string> formatProblems)
    {
        this.taskIdsByRequirementId = taskIdsByRequirementId;
        this.formatProblems = formatProblems;
    }

    /// <summary>Gets the listed requirement IDs.</summary>
    public IReadOnlyCollection<string> RequirementIds => taskIdsByRequirementId.Keys;

    /// <summary>Reads the list from a file; a missing file lists nothing.</summary>
    /// <param name="path">The list's path.</param>
    /// <returns>The list.</returns>
    public static KnownFailures Load(string path) => Parse(File.Exists(path) ? File.ReadAllText(path) : string.Empty);

    /// <summary>Parses the list's text.</summary>
    /// <param name="text">The list's text.</param>
    /// <returns>The list; a malformed or repeated line is a problem <see cref="FindProblems"/> reports.</returns>
    public static KnownFailures Parse(string text)
    {
        Dictionary<string, string> taskIds = new(StringComparer.Ordinal);
        List<string> problems = [];
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string? problem = AddLine(lines[index].Trim(), index + 1, taskIds);
            if (problem is not null)
            {
                problems.Add(problem);
            }
        }

        return new KnownFailures(taskIds, problems);
    }

    private static string? AddLine(string line, int lineNumber, Dictionary<string, string> taskIds)
    {
        if (line.Length == 0 || line.StartsWith('#'))
        {
            return null;
        }

        string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 2 || !fields[1].StartsWith("BL-", StringComparison.Ordinal))
        {
            return $"KnownFailures.txt line {lineNumber} is not '<requirement id> BL-####': {line}";
        }

        return taskIds.TryAdd(fields[0], fields[1]) ? null : $"KnownFailures.txt lists {fields[0]} twice.";
    }
    /// <summary>Reports whether a requirement's case is listed as known to fail.</summary>
    /// <param name="requirementId">The requirement ID.</param>
    /// <returns><see langword="true"/> when it is listed.</returns>
    public bool Lists(string requirementId) => taskIdsByRequirementId.ContainsKey(requirementId);

    /// <summary>
    /// Checks the list against the tests: every line is well formed and names no ID twice, and
    /// every listed ID is carried by some test.
    /// </summary>
    /// <param name="carriers">Every test that carries a <see cref="SpecRequirementAttribute"/>.</param>
    /// <returns>One line per problem; empty when the list is sound.</returns>
    public IReadOnlyList<string> FindProblems(IReadOnlyList<SpecRequirementCarrier> carriers)
    {
        HashSet<string> carriedIds = carriers.SelectMany(carrier => carrier.RequirementIds).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. formatProblems,
            .. taskIdsByRequirementId.Keys
                .Where(id => !carriedIds.Contains(id))
                .Order(StringComparer.Ordinal)
                .Select(id => $"KnownFailures.txt lists {id}, which no test carries: remove its KnownFailures.txt line."),
        ];
    }

    /// <summary>
    /// Runs a conformance case. When none of its requirement IDs is listed, the case runs as is.
    /// When one is, a case failing an assertion passes, a case throwing anything else fails, and a passing case throws
    /// <see cref="KnownFailurePassedException"/> asking for its line to be removed.
    /// </summary>
    /// <param name="requirementIds">The requirement IDs the case carries.</param>
    /// <param name="runCase">The case.</param>
    /// <returns>A task that completes when the case has run.</returns>
    public async Task RunCaseAsync(IReadOnlyList<string> requirementIds, Func<Task> runCase)
    {
        string? listedId = requirementIds.FirstOrDefault(Lists);
        if (listedId is null)
        {
            await runCase().ConfigureAwait(false);
            return;
        }

        try
        {
            await runCase().ConfigureAwait(false);
        }
        catch (Exception exception) when (IsAssertionFailure(exception))
        {
            return;
        }

        throw new KnownFailurePassedException(
            $"{listedId} passes now: remove its KnownFailures.txt line ({taskIdsByRequirementId[listedId]}).");
    }

    /// <summary>
    /// Reports whether an exception is a failed assertion: MSTest's <c>UnitTestAssertException</c> or a
    /// type derived from it, named rather than referenced so this library takes no test framework. A
    /// listed case records a difference its assertions catch; any other exception, a crash included,
    /// still fails it.
    /// </summary>
    /// <param name="exception">The exception the case threw.</param>
    /// <returns><see langword="true"/> when it is a failed assertion.</returns>
    private static bool IsAssertionFailure(Exception exception)
    {
        for (Type? type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (type.FullName == AssertionFailureTypeName)
            {
                return true;
            }
        }

        return false;
    }
}
