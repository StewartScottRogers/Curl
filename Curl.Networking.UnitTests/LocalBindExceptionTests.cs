namespace Curl.Networking;

/// <summary>Pins the reason text of <see cref="LocalBindException" /> (BL-600).</summary>
[TestClass]
public sealed class LocalBindExceptionTests
{
    [TestMethod]
    [DataRow(true, "No error")]
    [DataRow(false, "Success")]
    public void ReasonText_GivesErrnoZerosWords(bool onWindows, string expected) =>
        Assert.AreEqual(expected, LocalBindException.ReasonText(onWindows));
}
