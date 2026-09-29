using System.Xml.Linq;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Guards the architecture rule that makes the protocol libraries independent: a protocol
/// library references only Abstractions and the hand-built libraries ADR-0120 lists, and
/// each hand-built library references only what its row of that ADR allows.
/// </summary>
[TestClass]
public sealed class ProtocolIsolationTests
{
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
        var violations = new List<string>();

        foreach (var project in ProtocolLibraries())
        {
            foreach (var referenced in ForbiddenProtocolReferences(ProjectReferences(project)))
            {
                violations.Add($"{ProjectName(project)} -> {referenced}");
            }
        }

        Assert.IsEmpty(
            violations,
            $"A protocol library may reference only {Abstractions} and the hand-built "
                + "libraries ADR-0120 lists: " + string.Join(", ", violations));
    }

    [TestMethod]
    public void HandBuiltLibrary_References_OnlyItsAdrRow()
    {
        var root = RepositoryRoot();
        var violations = new List<string>();

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
        Assert.IsEmpty(ForbiddenProtocolReferences([referenced]));
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
        var forbidden = ForbiddenProtocolReferences([Abstractions, referenced]);

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
        Assert.IsEmpty(ForbiddenHandBuiltReferences(library, [referenced]));
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
        var forbidden = ForbiddenHandBuiltReferences(library, [referenced]);

        Assert.HasCount(1, forbidden);
        Assert.AreEqual(referenced, forbidden[0]);
    }

    [TestMethod]
    public void Abstractions_References_Nothing()
    {
        var project = Path.Combine(RepositoryRoot(), Abstractions, Abstractions + ".csproj");

        var references = ProjectReferences(project);

        Assert.IsEmpty(
            references,
            $"{Abstractions} must reference nothing: " + string.Join(", ", references));
    }

    [TestMethod]
    public void EveryProtocolLibrary_HasAMatchingTestProject()
    {
        var root = RepositoryRoot();

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

    private static IEnumerable<string> ProtocolLibraries() =>
        Directory.EnumerateFiles(RepositoryRoot(), "Curl.Protocol.*.UnitLibrary.csproj",
                SearchOption.AllDirectories)
            .Where(path => ProjectName(path) != Abstractions);

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
