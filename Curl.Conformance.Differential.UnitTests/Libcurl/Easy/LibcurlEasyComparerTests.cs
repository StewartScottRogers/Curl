using System.Text;
using Curl.Conformance.Differential.Libcurl.Easy;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Easy;

[TestClass]
public sealed class LibcurlEasyComparerTests
{
    [TestMethod]
    public void Compare_SameRunsWithDifferentTimesAndPorts_HasNoDifferences()
    {
        var real = Line("after 300843 milliseconds", "|200028=0:51000");
        var ours = Line("after 12 ms", "|200028=0:52000");

        var outcome = LibcurlEasyComparer.Compare("a", [real], [ours], "cnonce=\"1\" response=\"2\""u8.ToArray(), "cnonce=\"3\" response=\"4\""u8.ToArray());

        Assert.AreEqual("a", outcome.Name);
        Assert.IsEmpty(outcome.Differences);
    }

    [TestMethod]
    public void Compare_DifferentSegments_ReportsTheFirstFour()
    {
        var real = "0 rc=0 |1=0:a |2=0:a |3=0:a |4=0:a |5=0:a";
        var ours = "0 rc=0 |1=0:b |2=0:b |3=0:b |4=0:b |5=0:b";

        var difference = LibcurlEasyComparer.Compare("a", [real], [ours], [], []).Differences.Single();

        Assert.AreEqual("[('1=0:a', '1=0:b'), ('2=0:a', '2=0:b'), ('3=0:a', '3=0:b'), ('4=0:a', '4=0:b')]", difference);
    }

    [TestMethod]
    public void Compare_DifferentRoundCountAndWire_ReportsBoth()
    {
        var differences = LibcurlEasyComparer.Compare("a", ["0 rc=0", "1 rc=0"], ["0 rc=0"], "GET"u8.ToArray(), "PUT"u8.ToArray()).Differences;

        CollectionAssert.AreEqual(new[] { "['ROUNDS', 2, 1]", "['WIRE', b'GET', b'PUT']" }, differences.ToArray());
    }

    [TestMethod]
    public void Compare_LongWire_CutsTheLineAt900() =>
        Assert.HasCount(900, LibcurlEasyComparer.Compare("a", [], [], new byte[600], new byte[601]).Differences.Single());

    private static string Line(string error, string port) =>
        $"0 rc=28 err={Convert.ToHexString(Encoding.Latin1.GetBytes("timed out " + error))} set=[] body= hdr= {port}";
}
