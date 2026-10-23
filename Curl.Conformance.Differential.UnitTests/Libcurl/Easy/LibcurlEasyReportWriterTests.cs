using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Easy;

[TestClass]
public sealed class LibcurlEasyReportWriterTests
{
    private static readonly LibcurlEasyCaseOutcome[] Outcomes = [new("same", []), new("httpver-2", ["d1", "d2"])];

    [TestMethod]
    public void Write_WritesOnlyTheCasesThatDiffer() =>
        Assert.AreEqual("### httpver-2\nd1\nd2\n\n", LibcurlEasyReportWriter.Write(Outcomes));

    [TestMethod]
    public void Summary_CountsTheCases() =>
        Assert.AreEqual("cases 2 identical 1 different 1", LibcurlEasyReportWriter.Summary(Outcomes));
}
