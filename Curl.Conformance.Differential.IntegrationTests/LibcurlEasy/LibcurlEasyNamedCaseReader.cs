using System.Text.Json;
using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlEasy;

/// <summary>
/// Reads the named easy-handle cases embedded at <c>Cases/LibcurlEasyCases/EasyCases.json</c>,
/// the tester's <c>easytest.py</c> case list: a JSON array of <c>{"name", "rounds"}</c>, each
/// case's rounds read with <see cref="LibcurlEasyRoundReader"/>.
/// </summary>
internal static class LibcurlEasyNamedCaseReader
{
    /// <summary>The embedded resource name of the case list.</summary>
    internal const string ResourceName = "Curl.Conformance.Differential.Cases.LibcurlEasyCases.EasyCases.json";

    /// <summary>Reads every case in the embedded case list, in file order.</summary>
    /// <returns>The cases, each by its <c>easytest.py</c> name.</returns>
    internal static IReadOnlyList<LibcurlEasyCase> ReadEmbedded()
    {
        using var stream = typeof(LibcurlEasyRoundReader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException($"No case list \"{ResourceName}\" is embedded.", ResourceName);
        using var document = JsonDocument.Parse(stream);
        return [.. document.RootElement.EnumerateArray().Select(easyCase => new LibcurlEasyCase(
            easyCase.GetProperty("name").GetString()!,
            LibcurlEasyRoundReader.Read(easyCase.GetProperty("rounds").GetRawText())))];
    }
}
