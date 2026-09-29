namespace Curl.Networking;

/// <summary>Pins <see cref="DnsMessageFailureText" /> to curl 8.21.0's <c>doh_strerror</c> table.</summary>
[TestClass]
public sealed class DnsMessageFailureTextTests
{
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
        Assert.AreEqual(expected, DnsMessageFailureText.Describe(failure));
    }

    [TestMethod]
    public void Describe_AnUndefinedValue_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, DnsMessageFailureText.Describe((DnsMessageFailure)99));
    }
}
