using System.Text;
using Curl.Conformance.Extended.GoogleCloudStorage;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.GoogleCloudStorage;

/// <summary>The reference endpoint's own answers to requests curl's cases never send.</summary>
[TestClass]
public sealed class GcsReferenceServerTests
{
    private static GcsReferenceServer ServerWithOneObject()
    {
        GcsReferenceServer server = new(new GcsAnonymousReferenceAuthorizer());
        server.PutObject("b", "o", "x"u8.ToArray());
        return server;
    }

    private static GcsReferenceResponse Answer(string head) =>
        ServerWithOneObject().Answer(GcsReferenceRequest.ParseHead(head));

    [TestMethod]
    [DataRow("DELETE /b/o HTTP/1.1\r\nHost: h", 405, "MethodNotAllowed")]
    [DataRow("PUT /b/o HTTP/1.1\r\nHost: h", 411, "MissingContentLength")]
    [DataRow("PUT /b HTTP/1.1\r\nHost: h\r\nContent-Length: 0", 411, "MissingContentLength")]
    [DataRow("GET /b/o HTTP/1.1", 400, "InvalidArgument")]
    [DataRow("GET /nope/o HTTP/1.1\r\nHost: h", 404, "NoSuchBucket")]
    public void Answer_RequestTheApiRefuses_SendsTheErrorBody(string head, int statusCode, string code)
    {
        GcsReferenceResponse response = Answer(head);

        Assert.AreEqual(statusCode, response.StatusCode);
        StringAssert.Contains(Encoding.UTF8.GetString(response.Body), $"<Error><Code>{code}</Code>");
    }

    [TestMethod]
    public void Answer_ListWithoutDelimiterOrPrefix_ListsEveryObject()
    {
        GcsReferenceResponse response = Answer("GET /b?list-type=2&flag HTTP/1.1\r\nHost: h");

        Assert.AreEqual(200, response.StatusCode);
        StringAssert.Contains(Encoding.UTF8.GetString(response.Body), "<Contents><Key>o</Key>");
    }

    [TestMethod]
    public void ParseHead_ParameterWithoutValue_HasAnEmptyValue()
    {
        GcsReferenceRequest request = GcsReferenceRequest.ParseHead("GET /b?flag HTTP/1.1");

        Assert.AreEqual(string.Empty, request.Query["flag"]);
        Assert.IsNull(request.ContentLength);
    }

    [TestMethod]
    public void Refuse_WrongBearerToken_Sends401()
    {
        GcsReferenceRequest request = GcsReferenceRequest.ParseHead("GET /b/o HTTP/1.1\r\nAuthorization: Bearer other");

        Assert.AreEqual(401, new GcsBearerReferenceAuthorizer("right").Refuse(request)!.StatusCode);
    }

    [TestMethod]
    public async Task ServeAsync_BodyCutShortByTheClose_StoresWhatWasSent()
    {
        GcsReferenceServer server = ServerWithOneObject();
        using MemoryStream stream = new();
        stream.Write("PUT /b/n HTTP/1.1\r\nHost: h\r\nContent-Length: 10\r\n\r\nabc"u8);
        stream.Position = 0;

        await server.ServeAsync(new ReferenceConnection(stream, new WireExchange(1, ReferenceTransport.Tcp)), CancellationToken.None);

        Assert.AreEqual("abc", Encoding.UTF8.GetString(server.GetObject("b", "n")!));
        Assert.IsNull(server.GetObject("none", "n"));
    }
}
