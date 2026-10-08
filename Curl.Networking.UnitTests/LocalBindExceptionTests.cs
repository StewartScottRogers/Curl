using Curl.Testing;

namespace Curl.Networking;

/// <summary>Pins the reason text of <see cref="LocalBindException" /> (BL-600).</summary>
[TestClass]
public sealed class LocalBindExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(true, "No error")]
    [DataRow(false, "Success")]
    public void ReasonText_GivesErrnoZerosWords(bool onWindows, string expected)
    {
        Diagnostics.Arrange("on Windows", onWindows);

        var actual = LocalBindException.ReasonText(onWindows);

        Diagnostics.Act("reason text", actual);
        Diagnostics.Assert("reason text", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
