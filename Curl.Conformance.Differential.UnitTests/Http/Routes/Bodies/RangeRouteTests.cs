using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="RangeRoute"/>.</summary>
[TestClass]
public sealed class RangeRouteTests
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    [TestMethod]
    public async Task RespondAsync_NoRange_AnswersWholeBodyWithAcceptRangesAndKeepsTheConnection()
    {
        var whole = string.Concat(Enumerable.Repeat(Alphabet, 4));

        var written = await RespondAsync(new RangeRoute(), "GET /range HTTP/1.1\r\n\r\nGET /range HTTP/1.1\r\n\r\n");

        Assert.AreEqual(string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Accept-Ranges: bytes\r\nContent-Length: 104\r\n\r\n{whole}", 2)), written);
    }

    [TestMethod]
    [DataRow("bytes=2-4", "2-4", "CDE")]
    [DataRow("bytes=100-", "100-103", "WXYZ")]
    [DataRow("bytes=-5", "0-5", "ABCDEF")]
    [DataRow("bytes=24-27,30-31", "24-27", "YZAB")]
    [DataRow("bytes=102-500", "102-500", "YZ")]
    [DataRow("bytes=200-300", "200-300", "")]
    [DataRow("bytes=5-2", "5-2", "")]
    public async Task RespondAsync_Range_AnswersPartialContentSlicedAsPythonDoes(string range, string contentRange, string body)
    {
        var written = await RespondAsync(new RangeRoute(), $"GET /range HTTP/1.1\r\nRange: {range}\r\n\r\n");

        Assert.AreEqual(
            $"HTTP/1.1 206 Partial Content\r\n{Date}Content-Range: bytes {contentRange}/104\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\n\r\n{body}",
            written);
    }

    [TestMethod]
    [DataRow("items=1-2")]
    [DataRow("bytes=1x")]
    [DataRow("bytes=99999999999999999999-1")]
    [DataRow("bytes=1-99999999999999999999")]
    public async Task RespondAsync_RangeNotUnderstood_ClosesUnanswered(string range)
    {
        var written = await RespondAsync(new RangeRoute(), $"GET /range HTTP/1.1\r\nRange: {range}\r\n\r\nGET /range HTTP/1.1\r\n\r\n");

        Assert.AreEqual(string.Empty, written);
    }

    [TestMethod]
    public async Task RespondAsync_EmptyRange_AnswersWholeBody()
    {
        using var connection = new MemoryStream();
        var request = new DifferentialHttpRequest("GET", "/range", string.Empty, new Dictionary<string, string> { ["range"] = string.Empty }, []);
        var context = new DifferentialRouteContext(request, connection, new DifferentialResponderState(), new DifferentialResponderOptions(18080, TimeProvider.System));

        var closes = await new RangeRoute().RespondAsync(context, CancellationToken.None);

        Assert.IsFalse(closes);
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}Accept-Ranges: bytes\r\nContent-Length: 104\r\n\r\n{string.Concat(Enumerable.Repeat(Alphabet, 4))}",
            Encoding.Latin1.GetString(connection.ToArray()));
    }
}
