using Curl.Testing;

namespace Curl.Networking;

/// <summary>Pins which <see cref="DnsQueryOutcome" /> ends a query and which moves it to the next server, as c-ares does (BL-694).</summary>
[TestClass]
public sealed class DnsQueryOutcomeTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(DnsLookupFailure.None, true)]
    [DataRow(DnsLookupFailure.NoData, true)]
    [DataRow(DnsLookupFailure.NotFound, true)]
    [DataRow(DnsLookupFailure.ServerFailure, false)]
    [DataRow(DnsLookupFailure.FormatError, false)]
    [DataRow(DnsLookupFailure.Refused, false)]
    [DataRow(DnsLookupFailure.BadReply, false)]
    [DataRow(DnsLookupFailure.Timeout, false)]
    [DataRow(DnsLookupFailure.Unreachable, false)]
    public void IsFinal_EachFailure_EndsTheQueryOrNot(DnsLookupFailure failure, bool expected)
    {
        Diagnostics.Arrange("failure", failure);

        var isFinal = new DnsQueryOutcome(null, failure).IsFinal;

        Diagnostics.Act("is final", isFinal);
        Diagnostics.Assert("is final", expected, isFinal);
        Assert.AreEqual(expected, new DnsQueryOutcome(null, failure).IsFinal);
    }
}
