namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// One extended protocol's requirement matrix, <c>Documentation/Conformance/Extended/&lt;Name&gt;.md</c>:
/// one Markdown table of <see cref="RequirementMatrixRow"/>s. The test project copies the matrices
/// into its output, so <see cref="LoadAll"/> reads them there rather than from the repository.
/// </summary>
/// <param name="ProtocolName">The protocol name, the matrix file's name without <c>.md</c>.</param>
/// <param name="Rows">The table's rows, header and separator excluded.</param>
public sealed record RequirementMatrix(string ProtocolName, IReadOnlyList<RequirementMatrixRow> Rows)
{
    /// <summary>The prefix of a <c>Covered by</c> cell that names no test, followed by the reason.</summary>
    public const string NotApplicablePrefix = "n/a:";

    /// <summary>Reads every <c>*.md</c> file in a folder as a matrix named by its file name.</summary>
    /// <param name="directory">The folder; a missing folder holds no matrices.</param>
    /// <returns>The matrices, ordered by protocol name.</returns>
    public static IReadOnlyList<RequirementMatrix> LoadAll(string directory) =>
        !Directory.Exists(directory)
            ? []
            : Directory.GetFiles(directory, "*.md")
                .Order(StringComparer.Ordinal)
                .Select(path => Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))
                .ToList();

    /// <summary>Parses a matrix's Markdown: every line starting with <c>|</c> other than the header and separator is a row.</summary>
    /// <param name="protocolName">The protocol name.</param>
    /// <param name="markdown">The matrix file's text.</param>
    /// <returns>The matrix; a missing cell reads as empty.</returns>
    public static RequirementMatrix Parse(string protocolName, string markdown)
    {
        List<RequirementMatrixRow> rows = [];
        foreach (string line in markdown.Split('\n'))
        {
            string[]? cells = TableCellsOf(line.Trim());
            if (cells is not null && !IsHeaderOrSeparator(cells))
            {
                rows.Add(new RequirementMatrixRow(CellAt(cells, 0), CellAt(cells, 1), CellAt(cells, 2), CellAt(cells, 3)));
            }
        }

        return new RequirementMatrix(protocolName, rows);
    }

    /// <summary>
    /// Checks the matrices against the tests: no matrix is empty, no ID is a row twice, every row
    /// names a test that exists and carries its ID (or says <c>n/a: &lt;reason&gt;</c>), and every
    /// carried ID is a row of its protocol's matrix.
    /// </summary>
    /// <param name="matrices">Every matrix.</param>
    /// <param name="carriers">Every test that carries a <see cref="SpecRequirementAttribute"/>.</param>
    /// <returns>One line per problem; empty when the matrices and tests agree.</returns>
    public static IReadOnlyList<string> FindProblems(IReadOnlyList<RequirementMatrix> matrices, IReadOnlyList<SpecRequirementCarrier> carriers)
    {
        Dictionary<string, SpecRequirementCarrier> testsByName = carriers.ToDictionary(carrier => carrier.TestName, StringComparer.Ordinal);
        List<string> problems = [];
        foreach (RequirementMatrix matrix in matrices)
        {
            problems.AddRange(matrix.FindRowProblems(testsByName));
        }

        problems.AddRange(FindUnlistedIds(matrices, carriers));
        return problems;
    }

    private IEnumerable<string> FindRowProblems(Dictionary<string, SpecRequirementCarrier> testsByName)
    {
        if (Rows.Count == 0)
        {
            yield return $"{ProtocolName}.md has no requirement rows.";
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (RequirementMatrixRow row in Rows)
        {
            if (!seen.Add(row.Id))
            {
                yield return $"{row.Id} is a row of {ProtocolName}.md twice.";
            }

            string? problem = FindCoverageProblem(row, testsByName);
            if (problem is not null)
            {
                yield return problem;
            }
        }
    }

    private string? FindCoverageProblem(RequirementMatrixRow row, Dictionary<string, SpecRequirementCarrier> testsByName)
    {
        if (row.CoveredBy.StartsWith(NotApplicablePrefix, StringComparison.Ordinal))
        {
            return row.CoveredBy.Length > NotApplicablePrefix.Length && row.CoveredBy[NotApplicablePrefix.Length..].Trim().Length > 0
                ? null
                : $"{row.Id} in {ProtocolName}.md is n/a with no reason.";
        }

        if (!testsByName.TryGetValue(row.CoveredBy, out SpecRequirementCarrier? test))
        {
            return $"{row.Id} in {ProtocolName}.md is covered by '{row.CoveredBy}', which is no test carrying [SpecRequirement].";
        }

        return test.RequirementIds.Contains(row.Id, StringComparer.Ordinal)
            ? null
            : $"{row.Id} in {ProtocolName}.md is covered by {row.CoveredBy}, which does not carry [SpecRequirement(\"{row.Id}\")].";
    }

    private static IEnumerable<string> FindUnlistedIds(IReadOnlyList<RequirementMatrix> matrices, IReadOnlyList<SpecRequirementCarrier> carriers)
    {
        HashSet<string> matrixIds = matrices.SelectMany(matrix => matrix.Rows.Select(row => row.Id).Where(id => SpecRequirementAttribute.ProtocolNameOf(id) == matrix.ProtocolName)).ToHashSet(StringComparer.Ordinal);
        return carriers
            .SelectMany(carrier => carrier.RequirementIds.Select(id => (Id: id, carrier.TestName)))
            .Where(carried => !matrixIds.Contains(carried.Id))
            .Select(carried => $"{carried.Id}, carried by {carried.TestName}, is no row of {SpecRequirementAttribute.ProtocolNameOf(carried.Id)}.md.");
    }

    private static string[]? TableCellsOf(string line) =>
        line.StartsWith('|')
            ? line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray()
            : null;

    private static bool IsHeaderOrSeparator(string[] cells) =>
        cells[0] == "ID" || cells[0].TrimStart(':').StartsWith('-');

    private static string CellAt(string[] cells, int index) => index < cells.Length ? cells[index] : string.Empty;
}
