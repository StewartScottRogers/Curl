using Curl.Extension.OpenTelemetry.TraceContext;

namespace Curl.Extension.OpenTelemetry.UnitTests.TraceContext;

[TestClass]
public sealed class TraceParentTests
{
    private const string Valid = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    [TestMethod]
    public void Parse_TheSpecificationsExample_ReadsEachField_W3cTraceContextSection3Point2Point2()
    {
        var parsed = TraceParent.Parse(" " + Valid + "\t");

        Assert.AreEqual(new TraceParent("4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7", "01"), parsed);
        Assert.AreEqual(Valid, parsed!.ToString());
    }

    [TestMethod]
    public void Parse_ALaterVersionWithMoreFields_ReadsTheVersion00Fields_W3cTraceContextSection3Point2Point4()
    {
        Assert.AreEqual("00f067aa0ba902b7", TraceParent.Parse("cc-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01-what-the-future-holds")!.ParentId);
        Assert.AreEqual("01", TraceParent.Parse("01-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")!.Flags);
    }

    [TestMethod]
    [DataRow("ff-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", DisplayName = "version ff is invalid")]
    [DataRow("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01-extra", DisplayName = "version 00 is exactly 55 characters")]
    [DataRow("cc-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01x", DisplayName = "a later version's extra fields follow a dash")]
    [DataRow("00-4BF92F3577B34DA6A3CE929D0E0E4736-00f067aa0ba902b7-01", DisplayName = "upper-case hex is invalid")]
    [DataRow("00-00000000000000000000000000000000-00f067aa0ba902b7-01", DisplayName = "an all-zero trace-id is invalid")]
    [DataRow("00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01", DisplayName = "an all-zero parent-id is invalid")]
    [DataRow("0g-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", DisplayName = "the version is hex")]
    [DataRow("00_4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", DisplayName = "a dash follows the version")]
    [DataRow("00-4bf92f3577b34da6a3ce929d0e0e473x-00f067aa0ba902b7-01", DisplayName = "the trace-id is hex")]
    [DataRow("00-4bf92f3577b34da6a3ce929d0e0e4736_00f067aa0ba902b7-01", DisplayName = "a dash follows the trace-id")]
    [DataRow("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902bx-01", DisplayName = "the parent-id is hex")]
    [DataRow("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7_01", DisplayName = "a dash follows the parent-id")]
    [DataRow("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-0x", DisplayName = "the flags are hex")]
    [DataRow("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-1", DisplayName = "too short")]
    public void Parse_AnInvalidValue_GivesNull_W3cTraceContextSections3Point2Point2And3Point2Point4(string value)
    {
        Assert.IsNull(TraceParent.Parse(value));
    }

    [TestMethod]
    public void Fresh_DrawsAgainWhileAnIdIsAllZeros_AndIsSampled_W3cTraceContextSections3Point2Point2Point3To3Point2Point2Point5()
    {
        var draws = 0;
        var fresh = TraceParent.Fresh(bytes =>
        {
            bytes.Fill(draws++ % 2 == 0 ? (byte)0 : (byte)0xab);
        });

        Assert.AreEqual("00-abababababababababababababababab-abababababababab-01", fresh.ToString());
        Assert.AreEqual(4, draws);
    }
}
