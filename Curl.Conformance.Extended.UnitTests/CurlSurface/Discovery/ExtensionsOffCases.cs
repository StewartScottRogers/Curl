using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Discovery.DiscoveryCaseRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Discovery;

/// <summary><c>CURL_EXTENSIONS=0</c> turns every extension off: its schemes and options are what curl makes of ones it does not know (ADR-0999, ADR-1142).</summary>
[TestClass]
public sealed class ExtensionsOffCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:Off-1")]
    public Task EveryExtendedScheme_WithExtensionsOff_IsRefusedAsAnUnknownSchemeIs() =>
        Known.RunCaseAsync(
            ["CurlSurface:Off-1"],
            async () =>
            {
                CurlRunResult unknown = await RunAsync(Off, "-sS", "foo://x/");
                List<string> failures = [];

                foreach (string scheme in await ListedSchemesAsync())
                {
                    CurlRunResult refused = await RunAsync(Off, "-sS", $"{scheme}://x/");
                    string expected = ErrorOf(unknown).Replace("\"foo\"", $"\"{scheme}\"", StringComparison.Ordinal);
                    if (refused.ExitCode != unknown.ExitCode || ErrorOf(refused) != expected || refused.StandardOutput.Length != 0)
                    {
                        failures.Add($"{scheme}: exit {refused.ExitCode}, standard error '{ErrorOf(refused)}', expected exit {unknown.ExitCode} and '{expected}'");
                    }
                }

                Assert.AreEqual(1, unknown.ExitCode, ErrorOf(unknown));
                AssertNone("CurlSurface:Off-1", failures);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Off-2")]
    public Task EveryExtendedProtocolScheme_IsListedWithExtensionsOn() =>
        Known.RunCaseAsync(
            ["CurlSurface:Off-2"],
            async () =>
            {
                IReadOnlyList<string> listed = await ListedSchemesAsync();

                string[] missing = [.. ExtendedProtocols.Values.SelectMany(schemes => schemes).Where(scheme => !listed.Contains(scheme))];

                AssertNone("CurlSurface:Off-2", missing);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Off-3")]
    public Task EveryExtensionOption_WithExtensionsOff_IsRefusedAsAnUnknownOptionIs() =>
        Known.RunCaseAsync(
            ["CurlSurface:Off-3"],
            async () =>
            {
                const string Made = "--no-such-option-at-all";
                CurlRunResult unknown = await RunAsync(Off, Made, "x", "http://x/");
                List<string> failures = [];

                string[] options = [.. (await ExtendedHelpLinesAsync()).SelectMany(line => line.Options).Distinct(StringComparer.Ordinal)];
                Assert.IsNotEmpty(options, "CurlSurface:Off-3: --extended-help lists no option");
                foreach (string option in options)
                {
                    CurlRunResult refused = await RunAsync(Off, option, "x", "http://x/");
                    string expected = ErrorOf(unknown).Replace(Made, option, StringComparison.Ordinal);
                    if (refused.ExitCode != unknown.ExitCode || ErrorOf(refused) != expected)
                    {
                        failures.Add($"{option}: exit {refused.ExitCode}, standard error '{ErrorOf(refused)}', expected exit {unknown.ExitCode} and '{expected}'");
                    }
                }

                Assert.AreEqual(2, unknown.ExitCode, ErrorOf(unknown));
                AssertNone("CurlSurface:Off-3", failures);
            });
}
