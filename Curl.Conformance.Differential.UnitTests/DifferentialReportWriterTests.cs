namespace Curl.Conformance.Differential.UnitTests;

[TestClass]
public sealed class DifferentialReportWriterTests
{
    /// <summary>Lines 12 to 20 of the tester's 2026-10-22 report.txt, byte for byte.</summary>
    private const string ReportEntries =
        "### DIFF srv-te-trailers   args=-sS --raw -H TE: trailers {U}/chunked\n"
        + "exit: ours=TIMEOUT real=0\n"
        + "\n"
        + "### SLOW srv-te-trailers: ours 6.0s real 0.0s\n"
        + "\n"
        + "### DIFF redir-loc-multi-url   args=-sSL -o NUL -w %{url_effective}|%{http_code}|%{num_redirects}|%{redirect_url} {U}/loc-multi\n"
        + "stdout:\n"
        + "  ours=b'http://127.0.0.1:18080/loc-multi|000|0|'\n"
        + "  real=b'http://127.0.0.1:18080/loc-multi|302|0|'\n";

    [TestMethod]
    public void Write_ReportCases_MatchesTheTestersReportByteForByte()
    {
        var cases = DifferentialCaseFileReader.Read("""
            {"cases": [
              {"name": "srv-te-trailers", "command": "-sS --raw -H 'TE: trailers' {U}/chunked"},
              {"name": "identical", "command": "-s {U}/"},
              {"name": "redir-loc-multi-url", "command": "-sSL -o NUL -w '%{url_effective}|%{http_code}|%{num_redirects}|%{redirect_url}' {U}/loc-multi"}]}
            """);
        var outcomes = new[]
        {
            DifferentialComparer.Compare(
                cases[0],
                DifferentialComparerTests.Run(null, "", "", "") with { Elapsed = TimeSpan.FromSeconds(6.01) },
                DifferentialComparerTests.Run(0, "", "", "") with { Elapsed = TimeSpan.FromSeconds(0.02) }),
            DifferentialComparer.Compare(cases[1], DifferentialComparerTests.Run(0, "", "", ""), DifferentialComparerTests.Run(0, "", "", "")),
            DifferentialComparer.Compare(
                cases[2],
                DifferentialComparerTests.Run(0, "http://127.0.0.1:18080/loc-multi|000|0|", "", ""),
                DifferentialComparerTests.Run(0, "http://127.0.0.1:18080/loc-multi|302|0|", "", "")),
        };

        Assert.AreEqual(ReportEntries, DifferentialReportWriter.Write(outcomes));
    }

    [TestMethod]
    public void Write_DebugBuildLinesOnlyDifference_WritesNothing()
    {
        var differentialCase = DifferentialComparerTests.Case(true, true);
        var ours = DifferentialComparerTests.Run(0, "", "{ [41 bytes data]\r\n* closing connection #0\r\n", "");
        var real = DifferentialComparerTests.Run(0, "", "{ [41 bytes data]\r\n* sendrecv_dl() -> 23\r\n* Curl_sendrecv() -> 23\r\n* closing connection #0\r\n", "");

        var outcome = DifferentialComparer.Compare(differentialCase, DifferentialOutputNormaliser.Normalise(ours, "w"), DifferentialOutputNormaliser.Normalise(real, "w"));

        Assert.AreEqual(string.Empty, DifferentialReportWriter.Write([outcome]));
    }
}
