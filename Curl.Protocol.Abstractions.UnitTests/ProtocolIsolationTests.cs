using System.Xml.Linq;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Guards the architecture rule that makes the protocol libraries independent.
/// </summary>
[TestClass]
public sealed class ProtocolIsolationTests
{
    private const string Abstractions = "Curl.Protocol.Abstractions.UnitLibrary";

    [TestMethod]
    public void ProtocolLibrary_References_OnlyAbstractions()
    {
        var violations = new List<string>();

        foreach (var project in ProtocolLibraries())
        {
            foreach (var referenced in ProjectReferences(project))
            {
                if (referenced != Abstractions)
                {
                    violations.Add($"{ProjectName(project)} -> {referenced}");
                }
            }
        }

        Assert.IsEmpty(
            violations,
            $"A protocol library may reference only {Abstractions}: "
                + string.Join(", ", violations));
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
