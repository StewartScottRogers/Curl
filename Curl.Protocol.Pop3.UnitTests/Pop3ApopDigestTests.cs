namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins the <c>APOP</c> digest byte for byte: curl 8.21.0's for the recorder's timestamp and
/// <c>-u u:p</c> (BL-548), and RFC 1939 section 7's example.
/// </summary>
[TestClass]
public sealed class Pop3ApopDigestTests
{
    [TestMethod]
    public void Compute_RecordedTimestampAndPassword_IsCurlsDigest()
    {
        string digest = Pop3ApopDigest.Compute("<1896.697170952@localhost>", "p");

        Assert.AreEqual("d727ab40e6dcedbb6cf2f6735fe51cc8", digest);
    }

    [TestMethod]
    public void Compute_Rfc1939Example_IsTheRfcsDigest()
    {
        string digest = Pop3ApopDigest.Compute("<1896.697170952@dbc.mtview.ca.us>", "tanstaaf");

        Assert.AreEqual("c4c9334bac560ecc979e58001b3e22fb", digest);
    }
}
