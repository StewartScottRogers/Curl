using System.Xml.Linq;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Guards the architecture rule that makes the protocol libraries independent: a protocol
/// library references only Abstractions and the hand-built libraries ADR-0120 lists, and
/// each hand-built library references only what its row of that ADR allows.
/// </summary>
[TestClass]
public sealed class ProtocolIsolationTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Abstractions = "Curl.Protocol.Abstractions.UnitLibrary";
    private const string Cryptography = "Curl.Cryptography.UnitLibrary";
    private const string Tls = "Curl.Tls.UnitLibrary";
    private const string Http2 = "Curl.Http2.UnitLibrary";
    private const string Zstandard = "Curl.Zstandard.UnitLibrary";

    /// <summary>
    /// ADR-0120's table: each hand-built library a protocol library may reference, with the
    /// projects that hand-built library may itself reference.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> HandBuiltLibraries =
        new Dictionary<string, string[]>
        {
            [Cryptography] = [],
            ["Curl.Ntlm.UnitLibrary"] = [Cryptography],
            ["Curl.Kerberos.UnitLibrary"] = [Cryptography, Abstractions],
            [Zstandard] = [],
            [Tls] = [Cryptography, Zstandard, Abstractions],
            [Http2] = [Abstractions],
            ["Curl.Quic.UnitLibrary"] = [Tls, Cryptography, Abstractions],
            ["Curl.Http3.UnitLibrary"] = [Http2, Abstractions],
        };

    [TestMethod]
    public void ProtocolLibrary_References_OnlyAbstractionsAndHandBuiltLibraries()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var violations = new List<string>();
        diagnostics.Arrange("allowed protocol references", Abstractions + " and the ADR-0120 hand-built libraries");

        using (diagnostics.Phase("scan protocol projects"))
        {
            foreach (var project in ProtocolLibraries())
            {
                foreach (var referenced in ForbiddenProtocolReferences(ProjectReferences(project)))
                {
                    violations.Add($"{ProjectName(project)} -> {referenced}");
                }
            }
        }

        diagnostics.Act("violations", violations.Count + ": " + string.Join(", ", violations));
        diagnostics.Assert("violation count", 0, violations.Count);
        Assert.IsEmpty(
            violations,
            $"A protocol library may reference only {Abstractions} and the hand-built "
                + "libraries ADR-0120 lists: " + string.Join(", ", violations));
    }

    [TestMethod]
    public void HandBuiltLibrary_References_OnlyItsAdrRow()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var root = RepositoryRoot();
        var violations = new List<string>();
        diagnostics.Arrange("hand-built libraries in ADR-0120", HandBuiltLibraries.Count);

        using (diagnostics.Phase("scan hand-built projects"))
        {
            foreach (var library in HandBuiltLibraries.Keys)
            {
                var project = Path.Combine(root, library, library + ".csproj");

                if (!File.Exists(project))
                {
                    continue;
                }

                foreach (var referenced in ForbiddenHandBuiltReferences(library, ProjectReferences(project)))
                {
                    violations.Add($"{library} -> {referenced}");
                }
            }
        }

        diagnostics.Act("violations", violations.Count + ": " + string.Join(", ", violations));
        diagnostics.Assert("violation count", 0, violations.Count);
        Assert.IsEmpty(
            violations,
            "A hand-built library may reference only its row of ADR-0120: "
                + string.Join(", ", violations));
    }

    [TestMethod]
    [DataRow(Abstractions)]
    [DataRow("Curl.Cryptography.UnitLibrary")]
    [DataRow("Curl.Ntlm.UnitLibrary")]
    [DataRow("Curl.Kerberos.UnitLibrary")]
    [DataRow("Curl.Tls.UnitLibrary")]
    [DataRow("Curl.Http2.UnitLibrary")]
    [DataRow("Curl.Quic.UnitLibrary")]
    [DataRow("Curl.Http3.UnitLibrary")]
    [DataRow("Curl.Zstandard.UnitLibrary")]
    public void ForbiddenProtocolReferences_AllowedLibrary_IsNotForbidden(string referenced)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("referenced project", referenced);

        var forbidden = ForbiddenProtocolReferences([referenced]);

        diagnostics.Act("forbidden references", forbidden.Count + ": " + string.Join(", ", forbidden));
        diagnostics.Assert("forbidden count", 0, forbidden.Count);
        Assert.IsEmpty(forbidden);
    }

    [TestMethod]
    [DataRow("Curl.Protocol.Http.UnitLibrary")]
    [DataRow("Curl.Protocol.Ftp.UnitLibrary")]
    [DataRow("Curl.Networking.UnitLibrary")]
    [DataRow("Curl.Core.UnitLibrary")]
    [DataRow("Curl.Console")]
    [DataRow("Curl.Authentication.UnitLibrary")]
    [DataRow("Curl.Cli.UnitLibrary")]
    public void ForbiddenProtocolReferences_OtherProject_IsForbidden(string referenced)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("referenced project", referenced);

        var forbidden = ForbiddenProtocolReferences([Abstractions, referenced]);

        diagnostics.Act("forbidden references", forbidden.Count + ": " + string.Join(", ", forbidden));
        diagnostics.Assert("forbidden count", 1, forbidden.Count);
        Assert.HasCount(1, forbidden);
        Assert.AreEqual(referenced, forbidden[0]);
    }

    [TestMethod]
    [DataRow("Curl.Ntlm.UnitLibrary", "Curl.Cryptography.UnitLibrary")]
    [DataRow("Curl.Kerberos.UnitLibrary", Abstractions)]
    [DataRow("Curl.Tls.UnitLibrary", "Curl.Cryptography.UnitLibrary")]
    [DataRow("Curl.Tls.UnitLibrary", "Curl.Zstandard.UnitLibrary")]
    [DataRow("Curl.Http2.UnitLibrary", Abstractions)]
    [DataRow("Curl.Quic.UnitLibrary", "Curl.Tls.UnitLibrary")]
    [DataRow("Curl.Http3.UnitLibrary", "Curl.Http2.UnitLibrary")]
    public void ForbiddenHandBuiltReferences_ReferenceInItsRow_IsNotForbidden(
        string library, string referenced)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("library", library);
        diagnostics.Arrange("referenced project", referenced);

        var forbidden = ForbiddenHandBuiltReferences(library, [referenced]);

        diagnostics.Act("forbidden references", forbidden.Count + ": " + string.Join(", ", forbidden));
        diagnostics.Assert("forbidden count", 0, forbidden.Count);
        Assert.IsEmpty(forbidden);
    }

    [TestMethod]
    [DataRow("Curl.Cryptography.UnitLibrary", Abstractions)]
    [DataRow("Curl.Ntlm.UnitLibrary", Abstractions)]
    [DataRow("Curl.Http2.UnitLibrary", "Curl.Cryptography.UnitLibrary")]
    [DataRow("Curl.Http3.UnitLibrary", "Curl.Quic.UnitLibrary")]
    [DataRow("Curl.Tls.UnitLibrary", "Curl.Protocol.Http.UnitLibrary")]
    [DataRow("Curl.Quic.UnitLibrary", "Curl.Networking.UnitLibrary")]
    [DataRow("Curl.Kerberos.UnitLibrary", "Curl.Core.UnitLibrary")]
    [DataRow("Curl.Tls.UnitLibrary", "Curl.Console")]
    [DataRow("Curl.Zstandard.UnitLibrary", "Curl.Cryptography.UnitLibrary")]
    [DataRow("Curl.Zstandard.UnitLibrary", Abstractions)]
    public void ForbiddenHandBuiltReferences_ReferenceOutsideItsRow_IsForbidden(
        string library, string referenced)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("library", library);
        diagnostics.Arrange("referenced project", referenced);

        var forbidden = ForbiddenHandBuiltReferences(library, [referenced]);

        diagnostics.Act("forbidden references", forbidden.Count + ": " + string.Join(", ", forbidden));
        diagnostics.Assert("forbidden count", 1, forbidden.Count);
        Assert.HasCount(1, forbidden);
        Assert.AreEqual(referenced, forbidden[0]);
    }

    [TestMethod]
    public void Abstractions_References_Nothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("project", Abstractions + ".csproj");
        var project = Path.Combine(RepositoryRoot(), Abstractions, Abstractions + ".csproj");

        var references = ProjectReferences(project);

        diagnostics.Act("references", references.Count + ": " + string.Join(", ", references));
        diagnostics.Assert("reference count", 0, references.Count);
        Assert.IsEmpty(
            references,
            $"{Abstractions} must reference nothing: " + string.Join(", ", references));
    }

    [TestMethod]
    public void ProtocolLibraries_FindsTheProjectsUnderTheRepositoryRoot()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected present", "Curl.Protocol.Http.UnitLibrary");
        diagnostics.Arrange("expected absent", Abstractions);

        List<string> names;
        using (diagnostics.Phase("list protocol projects"))
        {
            names = ProtocolLibraries().Select(ProjectName).ToList();
        }

        diagnostics.Act("protocol library count", names.Count);
        diagnostics.Assert("contains Http", true, names.Contains("Curl.Protocol.Http.UnitLibrary"));
        diagnostics.Assert("contains Abstractions", false, names.Contains(Abstractions));
        CollectionAssert.Contains(names, "Curl.Protocol.Http.UnitLibrary");
        CollectionAssert.DoesNotContain(names, Abstractions);
    }

    [TestMethod]
    public void EveryProtocolLibrary_HasAMatchingTestProject()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var root = RepositoryRoot();
        var checkedTests = new List<string>();
        var missing = new List<string>();

        foreach (var project in ProtocolLibraries())
        {
            var tests = ProjectName(project).Replace(".UnitLibrary", ".UnitTests");
            checkedTests.Add(tests);

            if (!File.Exists(Path.Combine(root, tests, tests + ".csproj")))
            {
                missing.Add(tests);
            }
        }

        diagnostics.Arrange("test projects checked", checkedTests.Count);
        diagnostics.Act("missing test projects", missing.Count + ": " + string.Join(", ", missing));
        diagnostics.Assert("missing count", 0, missing.Count);

        foreach (var project in ProtocolLibraries())
        {
            var tests = ProjectName(project).Replace(".UnitLibrary", ".UnitTests");

            Assert.IsTrue(
                File.Exists(Path.Combine(root, tests, tests + ".csproj")),
                $"{tests} is missing.");
        }
    }

    /// <summary>
    /// Returns the references a protocol library may not have: everything except
    /// Abstractions and the hand-built libraries ADR-0120 lists.
    /// </summary>
    private static List<string> ForbiddenProtocolReferences(IEnumerable<string> references) =>
        references
            .Where(referenced => referenced != Abstractions
                && !HandBuiltLibraries.ContainsKey(referenced))
            .ToList();

    /// <summary>
    /// Returns the references the named hand-built library may not have: everything
    /// outside its row of ADR-0120.
    /// </summary>
    private static List<string> ForbiddenHandBuiltReferences(
        string library, IEnumerable<string> references) =>
        references
            .Where(referenced => !HandBuiltLibraries[library].Contains(referenced))
            .ToList();

    /// <summary>
    /// Returns every protocol library project other than Abstractions. Projects sit
    /// immediately under the repository root, so only that one level is read: walking
    /// deeper would enter other test projects' <c>bin</c> folders while their tests
    /// create and delete files there.
    /// </summary>
    private static IEnumerable<string> ProtocolLibraries() =>
        Directory.EnumerateDirectories(RepositoryRoot(), "Curl.Protocol.*.UnitLibrary",
                SearchOption.TopDirectoryOnly)
            .Select(folder => Path.Combine(folder, Path.GetFileName(folder) + ".csproj"))
            .Where(path => File.Exists(path) && ProjectName(path) != Abstractions);

    private static string ProjectName(string projectPath) =>
        Path.GetFileNameWithoutExtension(projectPath);

    private static IReadOnlyList<string> ProjectReferences(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .ToList();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Curl.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory);

        return directory!.FullName;
    }
}
