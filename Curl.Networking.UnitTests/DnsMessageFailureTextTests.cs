using Curl.Testing;

namespace Curl.Networking;

/// <summary>Pins <see cref="DnsMessageFailureText" /> to curl 8.21.0's <c>doh_strerror</c> table.</summary>
[TestClass]
public sealed class DnsMessageFailureTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(DnsMessageFailure.None, "")]
    [DataRow(DnsMessageFailure.BadLabel, "Bad label")]
    [DataRow(DnsMessageFailure.OutOfRange, "Out of range")]
    [DataRow(DnsMessageFailure.LabelLoop, "Label loop")]
    [DataRow(DnsMessageFailure.TooSmall, "Too small")]
    [DataRow(DnsMessageFailure.RdataLength, "RDATA length")]
    [DataRow(DnsMessageFailure.Malformed, "Malformat")]
    [DataRow(DnsMessageFailure.BadRcode, "Bad RCODE")]
    [DataRow(DnsMessageFailure.UnexpectedType, "Unexpected TYPE")]
    [DataRow(DnsMessageFailure.UnexpectedClass, "Unexpected CLASS")]
    [DataRow(DnsMessageFailure.NoContent, "No content")]
    [DataRow(DnsMessageFailure.BadId, "Bad ID")]
    [DataRow(DnsMessageFailure.NameTooLong, "Name too long")]
    public void Describe_ReturnsCurlsText(DnsMessageFailure failure, string expected)
    {
        Diagnostics.Arrange("failure", failure);

        var text = DnsMessageFailureText.Describe(failure);

        Diagnostics.Act("text", text);
        Diagnostics.Diff("text", expected, text);
        Assert.AreEqual(expected, DnsMessageFailureText.Describe(failure));
    }

    [TestMethod]
    public void Describe_AnUndefinedValue_ReturnsEmpty()
    {
        Diagnostics.Arrange("failure", 99);

        var text = DnsMessageFailureText.Describe((DnsMessageFailure)99);

        Diagnostics.Act("text", text);
        Diagnostics.Diff("text", string.Empty, text);
        Assert.AreEqual(string.Empty, DnsMessageFailureText.Describe((DnsMessageFailure)99));
    }
}
