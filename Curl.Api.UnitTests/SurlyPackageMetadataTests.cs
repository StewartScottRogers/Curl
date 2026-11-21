using System.Xml.Linq;

namespace Curl.Api;

/// <summary>
/// Pins what each Surly.* package's csproj says on nuget.org (BL-5708): a title, an icon, a licence
/// expression, a project URL, a first description sentence that says managed reimplementation with no
/// native libcurl, the differentiating tags, and a packed README that opens with the same statement.
/// </summary>
[TestClass]
public sealed class SurlyPackageMetadataTests
{
    private static readonly string[] ProjectNames = ["Curl.Api", "Libcurl", "Curl.Mcp"];

    private static string PackagingPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "SurlyPackaging", fileName);

    private static XElement Properties(string projectName) =>
        new("Properties", XDocument.Load(PackagingPath(projectName + ".csproj")).Root!.Elements("PropertyGroup").Elements());

    [TestMethod]
    public void EachProject_HasTitleIconLicenseExpressionAndProjectUrl()
    {
        foreach (string name in ProjectNames)
        {
            XElement properties = Properties(name);

            Assert.IsTrue(((string?)properties.Element("Title") ?? "").StartsWith("Surly ", StringComparison.Ordinal), name);
            Assert.AreEqual("SurlyIcon.png", (string?)properties.Element("PackageIcon"), name);
            Assert.AreEqual("curl", (string?)properties.Element("PackageLicenseExpression"), name);
            Assert.IsNull(properties.Element("PackageLicenseFile"), name);
            Assert.AreEqual("https://github.com/StewartScottRogers/Curl", (string?)properties.Element("PackageProjectUrl"), name);
            Assert.AreEqual("git", (string?)properties.Element("RepositoryType"), name);
            Assert.IsFalse(string.IsNullOrWhiteSpace((string?)properties.Element("Copyright")), name);
        }
    }

    [TestMethod]
    public void EachProject_PacksTheSharedIcon()
    {
        foreach (string name in ProjectNames)
        {
            XDocument project = XDocument.Load(PackagingPath(name + ".csproj"));

            Assert.IsTrue(
                project.Descendants("None").Any(item =>
                    ((string?)item.Attribute("Include") ?? "").EndsWith("SurlyIcon.png", StringComparison.Ordinal)
                    && (string?)item.Attribute("Pack") == "true"),
                name);
        }
    }

    [TestMethod]
    public void EachProject_DescriptionOpensWithManagedReimplementationAndNoNativeLibcurl()
    {
        foreach (string name in ProjectNames)
        {
            string description = (string?)Properties(name).Element("Description") ?? "";
            string firstSentence = description[..(description.IndexOf(". ", StringComparison.Ordinal) + 1)];

            StringAssert.Contains(firstSentence, "managed C# reimplementation of curl and libcurl", name);
            StringAssert.Contains(firstSentence, "no native libcurl", name);
        }
    }

    [TestMethod]
    public void EachProject_TagsSayManagedReimplementationNoNativeAndAot()
    {
        foreach (string name in ProjectNames)
        {
            string[] tags = ((string?)Properties(name).Element("PackageTags") ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string tag in new[] { "managed", "reimplementation", "no-native", "aot" })
            {
                CollectionAssert.Contains(tags, tag, name);
            }
        }
    }

    [TestMethod]
    public void EachPackedReadme_OpensWithTheStatementAndTheSurlyInstallLine()
    {
        foreach (string name in ProjectNames)
        {
            string[] lines = File.ReadAllLines(PackagingPath(name + ".README.md"));
            string firstParagraph = lines[2];

            StringAssert.StartsWith(firstParagraph, "A complete managed C# reimplementation of curl and libcurl for .NET 10: no native libcurl", name);
            StringAssert.Contains(firstParagraph, "Surly.", name);
        }
    }
}
