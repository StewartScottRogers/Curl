using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins how <see cref="UpstreamLineDifference"/> shows the lines where two outputs differ.</summary>
[TestClass]
public sealed class UpstreamLineDifferenceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Show_CrlfOnlyDifference_MarksBothLinesWithTheirLineEndsVisible()
    {
        string shown = Show("a\r\nb\r\nc\r\n", "a\r\nb\nc\r\n");

        Expect("  1: \"a\\r\\n\"\n- 2: \"b\\r\\n\"\n+ 2: \"b\\n\"\n  3: \"c\\r\\n\"\n", shown);
    }

    [TestMethod]
    public void Show_MissingTrailingLine_MarksOnlyTheExpectedLine()
    {
        string shown = Show("a\nb\n", "a\n");

        Expect("  1: \"a\\n\"\n- 2: \"b\\n\"\n", shown);
    }

    [TestMethod]
    public void Show_ExtraTrailingLine_MarksOnlyTheProducedLine()
    {
        string shown = Show("a\n", "a\nb");

        Expect("  1: \"a\\n\"\n+ 2: \"b\"\n", shown);
    }

    [TestMethod]
    public void Show_DifferenceDeepInside_ShowsTwoLinesOfContextEachSide()
    {
        string shown = Show("1\n2\n3\n4\nX\n6\n7\n8\n", "1\n2\n3\n4\nY\n6\n7\n8\n");

        Expect("  3: \"3\\n\"\n  4: \"4\\n\"\n- 5: \"X\\n\"\n+ 5: \"Y\\n\"\n  6: \"6\\n\"\n  7: \"7\\n\"\n", shown);
    }

    [TestMethod]
    public void Show_ManyChangedLines_ShowsFiveAndCountsTheRest()
    {
        string shown = Show("a\nb\nc\nd\ne\nf\ng\n", "\t\n");

        Expect("- 1: \"a\\n\"\n- 2: \"b\\n\"\n- 3: \"c\\n\"\n- 4: \"d\\n\"\n- 5: \"e\\n\"\n- ... 2 more\n+ 1: \"\\t\\n\"\n", shown);
    }

    private string Show(string expected, string actual)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("expected", Bytes(expected));
        diagnostics.Bytes("actual", Bytes(actual));

        string shown = UpstreamLineDifference.Show(Bytes(expected), Bytes(actual));

        diagnostics.Act("shown", shown);
        return shown;
    }

    private void Expect(string expected, string shown)
    {
        TestDiagnostics.For(TestContext).Diff("shown", expected, shown);
        Assert.AreEqual(expected, shown);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
