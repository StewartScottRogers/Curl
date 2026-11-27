using System.Xml.Linq;

namespace Curl.Api;

/// <summary>
/// Pins what each Surly.* package's csproj says on nuget.org (BL-5708): a title, an icon, a packed
/// licence file (nuget.org refuses the licence expression "curl"), a project URL, a first description
/// sentence that says managed reimplementation with no native libcurl, the differentiating tags, and a
/// packed README that opens with the same statement.
/// </summary>
[TestClass]
public sealed class SurlyPackageMetadataTests
{
    private static readonly string[] ProjectNames = ["Curl.Api", "Libcurl", "Curl.Mcp", "Curl.Console"];

    private static string PackagingPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "SurlyPackaging", fileName);

    private static XElement Properties(string projectName) =>
        new("Properties", XDocument.Load(PackagingPath(projectName + ".csproj")).Root!.Elements("PropertyGroup").Elements());

    private static bool Packs(string projectName, string fileName) =>
        XDocument.Load(PackagingPath(projectName + ".csproj")).Descendants("None").Any(item =>
            ((string?)item.Attribute("Include") ?? "").EndsWith(fileName, StringComparison.Ordinal)
            && (string?)item.Attribute("Pack") == "true");

    [TestMethod]
    public void EachProject_HasTitleIconLicenseFileAndProjectUrl()
    {
        foreach (string name in ProjectNames)
        {
            XElement properties = Properties(name);

            Assert.IsTrue(((string?)properties.Element("Title") ?? "").StartsWith("Surly ", StringComparison.Ordinal), name);
            Assert.AreEqual("SurlyIcon.png", (string?)properties.Element("PackageIcon"), name);
            Assert.AreEqual("LICENSE.txt", (string?)properties.Element("PackageLicenseFile"), name);
            Assert.IsNull(properties.Element("PackageLicenseExpression"), name);
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
            Assert.IsTrue(Packs(name, "SurlyIcon.png"), name);
        }
    }

    [TestMethod]
    public void EachProject_PacksTheSharedLicenseFile()
    {
        foreach (string name in ProjectNames)
        {
            Assert.IsTrue(Packs(name, "LICENSE.txt"), name);
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
    public void ConsoleProject_PacksSurlyCurlAsTheCurlToolForSixRuntimes()
    {
        XElement properties = Properties("Curl.Console");

        Assert.AreEqual("Surly.Curl", (string?)properties.Element("PackageId"));
        Assert.AreEqual("true", (string?)properties.Element("PackAsTool"));
        Assert.AreEqual("curl", (string?)properties.Element("ToolCommandName"));
        Assert.AreEqual("true", (string?)properties.Element("PublishAot"));
        Assert.AreEqual("win-x64;win-arm64;linux-x64;linux-arm64;osx-arm64;osx-x64", (string?)properties.Element("RuntimeIdentifiers"));
    }

    [TestMethod]
    public void ConsolePackedReadme_SaysHowToRunItWithDnxAndThatAGlobalInstallCanShadowTheSystemCurl()
    {
        string readme = File.ReadAllText(PackagingPath("Curl.Console.README.md"));

        StringAssert.Contains(readme, "dnx Surly.Curl@<version> --yes -- <curl arguments>");
        StringAssert.Contains(readme, "dotnet tool install -g Surly.Curl --prerelease");
        StringAssert.Contains(readme, "shadow the system curl on PATH");
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
