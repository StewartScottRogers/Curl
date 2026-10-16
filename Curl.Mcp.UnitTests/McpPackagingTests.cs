using System.Text.Json;
using System.Xml.Linq;

namespace Curl.Mcp;

/// <summary>
/// Holds Curl.Mcp's packing as an MCP server (BL-2388): the csproj properties Microsoft's
/// mcpserver template sets and the .mcp/server.json packed beside them, read from the copies
/// the build places under Packaging in the test output. The pack run itself belongs to
/// Curl.Mcp.IntegrationTests.
/// </summary>
[TestClass]
public sealed class McpPackagingTests
{
    private static string PackagingPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Packaging", fileName);

    private static XElement ProjectProperties()
    {
        XDocument project = XDocument.Load(PackagingPath("Curl.Mcp.csproj"));

        return new XElement("Properties", project.Root!.Elements("PropertyGroup").Elements());
    }

    private static JsonElement ServerJson(JsonDocument document) => document.RootElement;

    private static JsonDocument LoadServerJson() =>
        JsonDocument.Parse(File.ReadAllText(PackagingPath("server.json")));

    [TestMethod]
    public void Project_PacksAsAnMcpServerTool()
    {
        XElement properties = ProjectProperties();

        Assert.AreEqual("true", (string?)properties.Element("PackAsTool"));
        Assert.AreEqual("McpServer", (string?)properties.Element("PackageType"));
        Assert.AreEqual("Curl.Mcp", (string?)properties.Element("PackageId"));
        Assert.IsFalse(string.IsNullOrWhiteSpace((string?)properties.Element("ToolCommandName")));
        Assert.AreEqual("README.md", (string?)properties.Element("PackageReadmeFile"));
    }

    [TestMethod]
    public void Project_ListsSelfContainedRuntimeIdentifiers()
    {
        XElement properties = ProjectProperties();

        string[] runtimes = ((string?)properties.Element("RuntimeIdentifiers") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.IsNotEmpty(runtimes);
        CollectionAssert.Contains(runtimes, "win-x64");
        CollectionAssert.Contains(runtimes, "linux-x64");
        CollectionAssert.Contains(runtimes, "osx-arm64");
        Assert.AreEqual("true", (string?)properties.Element("PublishSelfContained"));
    }

    [TestMethod]
    public void Project_PacksServerJsonIntoTheMcpFolder()
    {
        XDocument project = XDocument.Load(PackagingPath("Curl.Mcp.csproj"));

        XElement? serverJson = project.Root!.Elements("ItemGroup").Elements("None")
            .SingleOrDefault(item => (string?)item.Attribute("Include") == @".mcp\server.json");

        Assert.IsNotNull(serverJson);
        Assert.AreEqual("true", (string?)serverJson.Attribute("Pack"));
        Assert.AreEqual("/.mcp/", (string?)serverJson.Attribute("PackagePath"));
    }

    [TestMethod]
    public void ServerJson_NamesTheRegistryEntryAndNuGetPackage()
    {
        using JsonDocument document = LoadServerJson();
        JsonElement server = ServerJson(document);

        JsonElement package = server.GetProperty("packages").EnumerateArray().Single();

        Assert.AreEqual("io.github.StewartScottRogers/Curl", server.GetProperty("name").GetString());
        Assert.AreEqual("nuget", package.GetProperty("registryType").GetString());
        Assert.AreEqual("Curl.Mcp", package.GetProperty("identifier").GetString());
        Assert.AreEqual("stdio", package.GetProperty("transport").GetProperty("type").GetString());
    }

    [TestMethod]
    public void ServerJson_RequiresTheRootAndNotTheResponseCap()
    {
        using JsonDocument document = LoadServerJson();
        JsonElement package = ServerJson(document).GetProperty("packages")[0];

        Dictionary<string, bool> required = package.GetProperty("environmentVariables").EnumerateArray()
            .ToDictionary(
                variable => variable.GetProperty("name").GetString()!,
                variable => variable.GetProperty("isRequired").GetBoolean());

        Assert.IsTrue(required[McpConfinementRoot.VariableName]);
        Assert.IsFalse(required[McpResponseCap.VariableName]);
    }

    [TestMethod]
    public void ServerJson_VersionMatchesTheProjectVersion()
    {
        string? projectVersion = (string?)ProjectProperties().Element("Version");
        using JsonDocument document = LoadServerJson();
        JsonElement server = ServerJson(document);

        Assert.IsFalse(string.IsNullOrWhiteSpace(projectVersion));
        Assert.AreEqual(projectVersion, server.GetProperty("version").GetString());
        Assert.AreEqual(projectVersion, server.GetProperty("packages")[0].GetProperty("version").GetString());
    }
}
