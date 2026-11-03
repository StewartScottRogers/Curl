using System.Reflection;
using System.Text.RegularExpressions;
using Curl.Conformance.Extended.Harness;
using Curl.Extension.Abstractions.Description;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>
/// Every scheme an extension registers has a row in the <c>CurlSurface-Transfer*.md</c> matrices for each of BL-4496's
/// checks, so a new scheme cannot skip the transfer surface. A row counts for a scheme when its ID, with the check taken
/// off its front or end, names the scheme (ignoring case, <c>-</c> and <c>+</c>), or when its requirement text names
/// the scheme in backticks; <c>demo</c> and <c>CurlProto</c> stand for the <c>curl-proto-&lt;scheme&gt;</c> helper scheme.
/// </summary>
[TestClass]
public sealed partial class TransferSurfaceSchemeCoverageTests
{
    /// <summary>The stand-in for a <c>curl-proto-&lt;scheme&gt;</c> helper scheme (ADR-1142), which no description lists.</summary>
    private const string HelperScheme = "demo";

    /// <summary>BL-4496's checks, as ID parts with case, <c>-</c> and <c>+</c> taken out.</summary>
    private static readonly string[] Checks =
    [
        "verbose", "output", "remotename", "writeout", "fail", "failwithbody", "maxtime", "connectionrefused",
        "unresolvablehost", "urlbadport", "urlbadpercentencoding", "urlspace", "silent", "showerror",
    ];

    /// <summary>Checks whose names begin with another check's, so the longest is taken off an ID first.</summary>
    private static readonly string[] LongestFirst = [.. Checks.Append("silentrefused").OrderByDescending(check => check.Length)];

    [TestMethod]
    public void EveryRegisteredScheme_HasARowForEveryTransferSurfaceCheck()
    {
        IReadOnlyList<RequirementMatrix> matrices = RequirementMatrix.LoadAll(Path.Combine(AppContext.BaseDirectory, "Conformance", "Extended"));
        IReadOnlyList<string> schemes = [.. RegisteredSchemes().Append(HelperScheme)];

        IReadOnlyList<string> missing = FindMissing(matrices, schemes);

        Assert.IsGreaterThan(30, schemes.Count, string.Join(' ', schemes));
        Assert.IsEmpty(missing, string.Join(Environment.NewLine, missing));
    }

    [TestMethod]
    public void FindMissing_SchemeWithoutARowForACheck_IsListed()
    {
        RequirementMatrix matrix = RequirementMatrix.Parse("CurlSurface-Transfer-Example", Header
            + string.Concat(Checks.Where(check => check != "maxtime").Select(check => $"| CurlSurface:{check}-redis-1 | MUST | x | n/a: y |\n")));

        IReadOnlyList<string> missing = FindMissing([matrix], ["redis"]);

        CollectionAssert.AreEqual(new[] { "redis has no CurlSurface-Transfer*.md row for maxtime." }, missing.ToArray());
    }

    [TestMethod]
    public void FindMissing_RowsNamedByIdEndOrBacktickedScheme_CoverTheScheme()
    {
        RequirementMatrix part = RequirementMatrix.Parse("CurlSurface-Transfer-Example", Header
            + string.Concat(Checks.Select((string check, int index) => (index % 3) switch
            {
                0 => $"| CurlSurface:Amqp0{check}-1 | MUST | `amqps`: x. | n/a: y |\n",
                1 => $"| CurlSurface:{check}-Amqps-1 | MUST | x. | n/a: y |\n",
                _ => $"| CurlSurface:{check}-amqps-2 | MUST | `-v` on `amqps`. | n/a: y |\n",
            })));

        Assert.IsEmpty(FindMissing([part], ["amqps"]));
    }

    [TestMethod]
    public void FindMissing_HelperRowsNamedCurlProto_CoverTheHelperScheme()
    {
        RequirementMatrix part = RequirementMatrix.Parse("CurlSurface-Transfer-Example", Header
            + string.Concat(Checks.Select(check => $"| CurlSurface:{check}-CurlProto-1 | MUST | x. | n/a: y |\n")));

        Assert.IsEmpty(FindMissing([part], [HelperScheme]));
    }

    [TestMethod]
    public void FindMissing_FailRowNamingFailWithBody_CoversBothChecksAndAnotherFailRowOnlyFail()
    {
        string rows = string.Concat(Checks.Where(check => check is not "fail" and not "failwithbody").Select(check => $"| CurlSurface:{check}-redis-1 | MUST | x | n/a: y |\n"));
        RequirementMatrix both = RequirementMatrix.Parse("CurlSurface-Transfer-Both", Header + rows + "| CurlSurface:Fail-Redis-1 | MUST | `--fail` and `--fail-with-body` leave it. | n/a: y |\n");
        RequirementMatrix failOnly = RequirementMatrix.Parse("CurlSurface-Transfer-FailOnly", Header + rows + "| CurlSurface:Fail-Redis-1 | MUST | `--fail` leaves it. | n/a: y |\n");

        Assert.IsEmpty(FindMissing([both], ["redis"]));
        CollectionAssert.AreEqual(new[] { "redis has no CurlSurface-Transfer*.md row for failwithbody." }, FindMissing([failOnly], ["redis"]).ToArray());
    }

    [TestMethod]
    public void FindMissing_RowsOutsideTheTransferSurfaceMatrices_DoNotCount()
    {
        RequirementMatrix other = RequirementMatrix.Parse("Redis", Header + "| CurlSurface:verbose-redis-1 | MUST | x | n/a: y |\n");

        Assert.HasCount(Checks.Length, FindMissing([other], ["redis"]));
    }

    private const string Header = "| ID | Level | Requirement | Covered by |\n|----|-------|-------------|------------|\n";

    /// <summary>The <c>&lt;scheme&gt; has no ... row for &lt;check&gt;.</c> line for each check a scheme has no transfer-surface row for.</summary>
    private static List<string> FindMissing(IReadOnlyList<RequirementMatrix> matrices, IReadOnlyList<string> schemes)
    {
        List<(string Check, string Remainder, string Requirement)> rows = [.. matrices
            .Where(matrix => matrix.ProtocolName.StartsWith("CurlSurface-Transfer", StringComparison.Ordinal))
            .SelectMany(matrix => matrix.Rows)
            .Select(row => Split(row))
            .OfType<(string Check, string Remainder, string Requirement)>()];

        return [.. schemes.SelectMany(scheme => Checks
            .Where(check => !rows.Any(row => Covers(row.Check, row.Requirement, check) && Names(row.Remainder, row.Requirement, scheme)))
            .Select(check => $"{scheme} has no CurlSurface-Transfer*.md row for {check}."))];
    }

    private static (string Check, string Remainder, string Requirement)? Split(RequirementMatrixRow row)
    {
        string body = Normalise(TrailingNumber().Replace(row.Id.Replace("CurlSurface:", string.Empty, StringComparison.Ordinal), string.Empty));
        string? check = LongestFirst.FirstOrDefault(check => body.StartsWith(check, StringComparison.Ordinal) || body.EndsWith(check, StringComparison.Ordinal));
        if (check is null)
        {
            return null;
        }

        string remainder = body.StartsWith(check, StringComparison.Ordinal) ? body[check.Length..] : body[..^check.Length];
        return (check, remainder, row.Requirement);
    }

    /// <summary>
    /// Whether a row whose ID names <paramref name="rowCheck" /> covers <paramref name="check" />: one <c>--fail</c> row
    /// whose requirement also names <c>--fail-with-body</c> covers both, as the process schemes' matrix writes them.
    /// </summary>
    private static bool Covers(string rowCheck, string requirement, string check) =>
        rowCheck == check
        || (rowCheck == "fail" && check == "failwithbody" && requirement.Contains("`--fail-with-body`", StringComparison.Ordinal));

    private static bool Names(string remainder, string requirement, string scheme) =>
        remainder == Normalise(scheme)
        || (scheme == HelperScheme && remainder == "curlproto")
        || requirement.Contains($"`{scheme}`", StringComparison.Ordinal);

    private static string Normalise(string text) => text.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal).Replace("+", string.Empty, StringComparison.Ordinal);

    /// <summary>The schemes every extension's description lists, read from the extension libraries in the test output.</summary>
    private static IEnumerable<string> RegisteredSchemes() =>
        Directory.GetFiles(AppContext.BaseDirectory, "Curl.Extended.Protocol.*.UnitLibrary.dll")
            .Concat(Directory.GetFiles(AppContext.BaseDirectory, "Curl.Extension.*.UnitLibrary.dll"))
            .Select(Assembly.LoadFrom)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            .Where(property => property.PropertyType == typeof(ExtensionDescription))
            .SelectMany(property => ((ExtensionDescription)property.GetValue(null)!).Schemes)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    [GeneratedRegex("-[0-9]+$")]
    private static partial Regex TrailingNumber();
}
