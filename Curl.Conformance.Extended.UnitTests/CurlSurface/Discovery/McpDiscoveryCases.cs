using System.Text.Json;
using Curl.Conformance.Extended.Harness;
using Curl.Extension.Composition;
using Curl.Mcp;
using Curl.Mcp.Protocol.Model;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Discovery.DiscoveryCaseRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Discovery;

/// <summary>
/// <c>Curl.Mcp</c>'s <c>parse_url</c>, <c>build_url</c> and <c>version</c> tools with every extended scheme, extensions
/// on and off (ADR-1181), through the host's own registry and the real extension handlers.
/// </summary>
[TestClass]
public sealed class McpDiscoveryCases
{
    private static readonly McpResponseCap Cap = new(McpResponseCap.DefaultMaxBytes);

    [TestMethod]
    [SpecRequirement("CurlSurface:Mcp-1")]
    public Task ParseAndBuildUrl_WithExtensionsOn_AcceptEveryExtendedSchemeInAnyCase() =>
        Known.RunCaseAsync(
            ["CurlSurface:Mcp-1"],
            async () =>
            {
                McpServerRegistry registry = Registry(On);
                List<string> failures = [];

                foreach (string scheme in await ListedSchemesAsync())
                {
                    foreach (string written in new[] { scheme, scheme.ToUpperInvariant() })
                    {
                        McpToolCallResult parsed = await CallAsync(registry, "parse_url", JsonSerializer.Serialize(new Dictionary<string, string> { ["url"] = $"{written}://example.com/a" }));
                        McpToolCallResult built = await CallAsync(registry, "build_url", JsonSerializer.Serialize(new Dictionary<string, string> { ["scheme"] = written, ["host"] = "example.com", ["path"] = "/a" }));
                        AddIfError(failures, $"parse_url {written}", parsed);
                        AddIfError(failures, $"build_url {written}", built);
                    }
                }

                AssertNone("CurlSurface:Mcp-1", failures);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Mcp-2")]
    public Task ParseAndBuildUrl_WithExtensionsOff_RefuseEveryExtendedSchemeAsUnsupported() =>
        Known.RunCaseAsync(
            ["CurlSurface:Mcp-2"],
            async () =>
            {
                McpServerRegistry registry = Registry(Off);
                List<string> failures = [];

                foreach (string scheme in await ListedSchemesAsync())
                {
                    McpToolCallResult parsed = await CallAsync(registry, "parse_url", JsonSerializer.Serialize(new Dictionary<string, string> { ["url"] = $"{scheme}://example.com/a" }));
                    McpToolCallResult built = await CallAsync(registry, "build_url", JsonSerializer.Serialize(new Dictionary<string, string> { ["scheme"] = scheme, ["host"] = "example.com", ["path"] = "/a" }));
                    AddIfNotUnsupported(failures, $"parse_url {scheme}", parsed);
                    AddIfNotUnsupported(failures, $"build_url {scheme}", built);
                }

                AssertNone("CurlSurface:Mcp-2", failures);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Mcp-3")]
    public Task Version_WithExtensionsOn_ListsEveryExtendedSchemeOnceInListingOrderApartFromCurlsProtocols() =>
        Known.RunCaseAsync(
            ["CurlSurface:Mcp-3"],
            async () =>
            {
                IReadOnlyList<string> listed = await ListedSchemesAsync();

                using JsonDocument version = JsonDocument.Parse((await CallAsync(Registry(On), "version", "{}")).Content[0].Text);

                CollectionAssert.AreEqual(listed.ToArray(), StringsOf(version.RootElement.GetProperty("extendedProtocols")), "CurlSurface:Mcp-3: extendedProtocols");
                string[] protocols = StringsOf(version.RootElement.GetProperty("protocols"));
                using JsonDocument versionOff = JsonDocument.Parse((await CallAsync(Registry(Off), "version", "{}")).Content[0].Text);
                CollectionAssert.AreEqual(StringsOf(versionOff.RootElement.GetProperty("protocols")), protocols, "CurlSurface:Mcp-3: protocols stays curl's");
                AssertNone("CurlSurface:Mcp-3", [.. ExtendedProtocols.Values.SelectMany(schemes => schemes).Where(protocols.Contains)]);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Mcp-4")]
    public Task Version_WithExtensionsOff_HasNoExtendedProtocols() =>
        Known.RunCaseAsync(
            ["CurlSurface:Mcp-4"],
            async () =>
            {
                using JsonDocument version = JsonDocument.Parse((await CallAsync(Registry(Off), "version", "{}")).Content[0].Text);

                Assert.AreEqual(0, version.RootElement.GetProperty("extendedProtocols").GetArrayLength(), "CurlSurface:Mcp-4");
            });

    private static McpServerRegistry Registry(IReadOnlyDictionary<string, string> environment) =>
        CurlMcpHost.CreateRegistry(McpConfinementRoot.NotConfigured, Cap, name => environment.GetValueOrDefault(name), ExtensionProtocolHandlers.Create);

    private static async Task<McpToolCallResult> CallAsync(McpServerRegistry registry, string tool, string arguments)
    {
        using JsonDocument document = JsonDocument.Parse(arguments);
        return await registry.FindTool(tool)!.CallAsync(document.RootElement, CancellationToken.None);
    }

    private static string[] StringsOf(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString()!)];

    private static void AddIfError(List<string> failures, string call, McpToolCallResult result)
    {
        if (result.IsError)
        {
            failures.Add($"{call}: {result.Content[0].Text}");
        }
    }

    private static void AddIfNotUnsupported(List<string> failures, string call, McpToolCallResult result)
    {
        if (!result.IsError || !result.Content[0].Text.Contains("UnsupportedScheme", StringComparison.Ordinal))
        {
            failures.Add($"{call}: {result.Content[0].Text}");
        }
    }
}
