using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsLookupFailureText" /> to c-ares 1.34.8's <c>ares_strerror</c> texts, the ones
/// curl 8.22.0's c-ares build printed after <c>Could not resolve host:</c> (measured, BL-694).
/// </summary>
[TestClass]
public sealed class DnsLookupFailureTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(DnsLookupFailure.None, "")]
    [DataRow(DnsLookupFailure.NoData, "DNS server returned answer with no data")]
    [DataRow(DnsLookupFailure.FormatError, "DNS server claims query was misformatted")]
    [DataRow(DnsLookupFailure.ServerFailure, "DNS server returned general failure")]
    [DataRow(DnsLookupFailure.NotFound, "Domain name not found")]
    [DataRow(DnsLookupFailure.NotImplemented, "DNS server does not implement requested operation")]
    [DataRow(DnsLookupFailure.Refused, "DNS server refused query")]
    [DataRow(DnsLookupFailure.BadName, "Misformatted domain name")]
    [DataRow(DnsLookupFailure.BadReply, "Misformatted DNS reply")]
    [DataRow(DnsLookupFailure.Unreachable, "Could not contact DNS servers")]
    [DataRow(DnsLookupFailure.Timeout, "Timeout while contacting DNS servers")]
    [DataRow(DnsLookupFailure.BadConfiguration, "Misformatted string")]
    [DataRow((DnsLookupFailure)99, "")]
    [DataRow((DnsLookupFailure)(-1), "")]
    public void Describe_EachFailure_IsCaresText(DnsLookupFailure failure, string expected)
    {
        Diagnostics.Arrange("failure", failure);

        var text = DnsLookupFailureText.Describe(failure);

        Diagnostics.Act("text", text);
        Diagnostics.Diff("text", expected, text);
        Assert.AreEqual(expected, DnsLookupFailureText.Describe(failure));
    }
}
