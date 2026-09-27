using System.Text;

namespace Curl.Conformance;

/// <summary>Builds parsed test cases from inline test-file text for the harness's own tests.</summary>
internal static class ParsedTestCase
{
    /// <summary>Parses the given sections wrapped in <c>&lt;testcase&gt;</c>.</summary>
    /// <param name="sections">Test-file text, such as <c>&lt;verify&gt;…&lt;/verify&gt;</c>.</param>
    /// <returns>The parsed case.</returns>
    public static UpstreamTestCase From(string sections) =>
        UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes($"<testcase>\n{sections}</testcase>\n")).TestCase!;
}
