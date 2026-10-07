using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins the <c>APOP</c> digest byte for byte: curl 8.21.0's for the recorder's timestamp and
/// <c>-u u:p</c> (BL-548), and RFC 1939 section 7's example.
/// </summary>
[TestClass]
public sealed class Pop3ApopDigestTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Compute_RecordedTimestampAndPassword_IsCurlsDigest()
    {
        Diagnostics.Arrange("timestamp and password", "<1896.697170952@localhost>, p");
        string digest = Pop3ApopDigest.Compute("<1896.697170952@localhost>", "p");
        Diagnostics.Act("digest", digest);

        Diagnostics.Diff("digest", "d727ab40e6dcedbb6cf2f6735fe51cc8", digest);
        Assert.AreEqual("d727ab40e6dcedbb6cf2f6735fe51cc8", digest);
    }

    [TestMethod]
    public void Compute_Rfc1939Example_IsTheRfcsDigest()
    {
        Diagnostics.Arrange("timestamp and password", "<1896.697170952@dbc.mtview.ca.us>, tanstaaf");
        string digest = Pop3ApopDigest.Compute("<1896.697170952@dbc.mtview.ca.us>", "tanstaaf");
        Diagnostics.Act("digest", digest);

        Diagnostics.Diff("digest", "c4c9334bac560ecc979e58001b3e22fb", digest);
        Assert.AreEqual("c4c9334bac560ecc979e58001b3e22fb", digest);
    }
}
