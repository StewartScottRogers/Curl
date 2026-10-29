using System.Text;

namespace Curl.Extension.SignalR.Negotiate;

[TestClass]
public sealed class NegotiateResponseReaderTests
{
    [TestMethod]
    public void Read_Version1_KeepsIdTokenAndTransports_NegotiateRequest()
    {
        NegotiateResponse response = Read("""
            {"connectionToken":"05265228-1e2c-46c5-82a1-6a5bcc3f0143","connectionId":"807809a5-31bf-470d-9e23-afaee35d8a0d","negotiateVersion":1,
             "availableTransports":[{"transport":"WebSockets","transferFormats":["Text","Binary"]},{"transport":"ServerSentEvents","transferFormats":["Text"]}]}
            """);

        Assert.AreEqual(1, response.NegotiateVersion);
        Assert.AreEqual("807809a5-31bf-470d-9e23-afaee35d8a0d", response.ConnectionId);
        Assert.AreEqual("05265228-1e2c-46c5-82a1-6a5bcc3f0143", response.ConnectionToken);
        Assert.HasCount(2, response.AvailableTransports);
        Assert.AreEqual("WebSockets", response.AvailableTransports[0].Transport);
        CollectionAssert.AreEqual(new[] { "Text", "Binary" }, response.AvailableTransports[0].TransferFormats.ToArray());
        Assert.IsTrue(response.OffersWebSockets);
        Assert.IsFalse(response.IsRedirect);
        Assert.IsNull(response.Error);
    }

    [TestMethod]
    public void Read_Version0_HasVersion0AndNoToken_NegotiateRequest()
    {
        NegotiateResponse response = Read("""{"connectionId":"807809a5","availableTransports":[{"transport":"LongPolling","transferFormats":["Text"]}]}""");

        Assert.AreEqual(0, response.NegotiateVersion);
        Assert.AreEqual("807809a5", response.ConnectionId);
        Assert.IsNull(response.ConnectionToken);
        Assert.IsFalse(response.OffersWebSockets);
    }

    [TestMethod]
    public void Read_RedirectForm_KeepsUrlAndAccessToken_NegotiateRequest()
    {
        NegotiateResponse response = Read("""{"url":"https://myapp.com/chat","accessToken":"accessToken"}""");

        Assert.IsTrue(response.IsRedirect);
        Assert.AreEqual("https://myapp.com/chat", response.Url);
        Assert.AreEqual("accessToken", response.AccessToken);
        Assert.IsEmpty(response.AvailableTransports);
    }

    [TestMethod]
    public void Read_ErrorForm_KeepsTheError_NegotiateRequest()
    {
        NegotiateResponse response = Read("""{"error":"This connection is not allowed."}""");

        Assert.AreEqual("This connection is not allowed.", response.Error);
        Assert.IsFalse(response.IsRedirect);
    }

    [TestMethod]
    [DataRow("""{"availableTransports":[{"transferFormats":["Text"]}]}""", DisplayName = "transport missing")]
    [DataRow("""{"availableTransports":[1]}""", DisplayName = "transport not an object")]
    [DataRow("""{"negotiateVersion":"1"}""", DisplayName = "version a string")]
    [DataRow("not json", DisplayName = "not JSON")]
    public void Read_NotANegotiateResponse_ThrowsWireFormatException_NegotiateRequest(string body)
    {
        Assert.ThrowsExactly<SignalRWireFormatException>(() => Read(body));
    }

    private static NegotiateResponse Read(string body) => NegotiateResponseReader.Read(Encoding.UTF8.GetBytes(body));
}
