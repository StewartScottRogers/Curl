using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Discovery.DiscoveryCaseRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Discovery;

/// <summary>
/// <c>--extended-help</c> and <c>--ai-help extensions</c> list every extension and say when they are off, and
/// <c>curl -V</c> stays curl's (ADR-0999, ADR-1008).
/// </summary>
[TestClass]
public sealed class ExtensionListingCases
{
    private const string MarkdownOffLine = "Extensions are off: `CURL_EXTENSIONS` is `0`.";

    [TestMethod]
    [SpecRequirement("CurlSurface:Listing-1")]
    public Task ExtendedHelp_WithExtensionsOn_ListsEveryExtendedProtocolWithItsSchemes() =>
        Known.RunCaseAsync(
            ["CurlSurface:Listing-1"],
            async () =>
            {
                IReadOnlyList<ExtendedHelpLine> lines = await ExtendedHelpLinesAsync();

                string[] failures =
                [
                    .. ExtendedProtocols
                        .Where(protocol => !lines.Any(line => line.Name == protocol.Key && line.Schemes.SequenceEqual(protocol.Value)))
                        .Select(protocol => $"{protocol.Key}: no line with schemes '{string.Join(", ", protocol.Value)}'"),
                ];

                AssertNone("CurlSurface:Listing-1", failures);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Listing-2")]
    public Task ExtendedHelp_WithExtensionsOff_SaysTheyAreOff() =>
        Known.RunCaseAsync(
            ["CurlSurface:Listing-2"],
            async () =>
            {
                CurlRunResult result = await RunAsync(Off, "--extended-help");

                Assert.AreEqual(0, result.ExitCode, "CurlSurface:Listing-2: exit code");
                Assert.AreEqual("Extensions are off: CURL_EXTENSIONS is 0.", OutputOf(result).TrimEnd('\r', '\n'), "CurlSurface:Listing-2");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Listing-3")]
    public Task AiHelpExtensions_WithExtensionsOn_HasASectionForEveryExtensionWithItsSchemes() =>
        Known.RunCaseAsync(
            ["CurlSurface:Listing-3"],
            async () =>
            {
                IReadOnlyList<ExtendedHelpLine> lines = await ExtendedHelpLinesAsync();
                CurlRunResult result = await RunAsync(On, "--ai-help", "extensions");
                string markdown = OutputOf(result);

                string[] failures =
                [
                    .. lines
                        .Where(line => !markdown.Contains($"\n## {line.Name}\n", StringComparison.Ordinal)
                            || !markdown.Contains($"- Schemes: {SchemesText(line.Schemes)}\n", StringComparison.Ordinal))
                        .Select(line => line.Name),
                ];

                Assert.AreEqual(0, result.ExitCode, "CurlSurface:Listing-3: exit code");
                Assert.IsFalse(markdown.Contains(MarkdownOffLine, StringComparison.Ordinal), "CurlSurface:Listing-3");
                AssertNone("CurlSurface:Listing-3", failures);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Listing-4")]
    public Task AiHelpExtensions_WithExtensionsOff_SaysTheyAreOffAndHasNoSection() =>
        Known.RunCaseAsync(
            ["CurlSurface:Listing-4"],
            async () =>
            {
                CurlRunResult result = await RunAsync(Off, "--ai-help", "extensions");
                string markdown = OutputOf(result);

                Assert.AreEqual(0, result.ExitCode, "CurlSurface:Listing-4: exit code");
                StringAssert.Contains(markdown, MarkdownOffLine, "CurlSurface:Listing-4");
                Assert.IsFalse(markdown.Contains("\n## ", StringComparison.Ordinal), $"CurlSurface:Listing-4: {markdown}");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Listing-5")]
    public Task Version_WithExtensionsOn_IsCurlsOwnAndNamesNoExtendedScheme() =>
        Known.RunCaseAsync(
            ["CurlSurface:Listing-5"],
            async () =>
            {
                CurlRunResult on = await RunAsync(On, "-V");
                CurlRunResult off = await RunAsync(Off, "-V");
                string protocolsLine = OutputOf(on).Split('\n').Single(line => line.StartsWith("Protocols:", StringComparison.Ordinal)).TrimEnd('\r');
                string[] protocols = protocolsLine.Split(' ')[1..];

                string[] leaked = [.. ExtendedProtocols.Values.SelectMany(schemes => schemes).Where(scheme => protocols.Contains(scheme, StringComparer.OrdinalIgnoreCase))];

                Assert.AreEqual(0, on.ExitCode, "CurlSurface:Listing-5: exit code");
                Assert.AreEqual(OutputOf(off), OutputOf(on), "CurlSurface:Listing-5: -V with extensions on and off");
                AssertNone("CurlSurface:Listing-5", leaked);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Listing-6")]
    public Task ExtendedHelp_PluginProtocolLine_ListsNoFixedScheme() =>
        Known.RunCaseAsync(
            ["CurlSurface:Listing-6"],
            async () =>
            {
                ExtendedHelpLine plugin = (await ExtendedHelpLinesAsync()).Single(line => line.Name == "plugin-protocol");

                Assert.IsEmpty(plugin.Schemes, plugin.Text);
                StringAssert.Contains(plugin.Text, " Schemes: none.", "CurlSurface:Listing-6");
            });

    private static string SchemesText(IReadOnlyList<string> schemes) =>
        schemes.Count == 0 ? "none" : string.Join(", ", schemes.Select(scheme => $"`{scheme}://`"));
}
