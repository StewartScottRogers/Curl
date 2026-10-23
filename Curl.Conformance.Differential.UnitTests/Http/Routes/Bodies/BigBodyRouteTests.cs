using System.Text;
using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="BigBodyRoute"/>.</summary>
[TestClass]
public sealed class BigBodyRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersThreeMillionBytesOfIndexModulo251ToEachAndKeepsTheConnection()
    {
        var body = new StringBuilder(3_000_000);
        for (var index = 0; index < 3_000_000; index++)
        {
            body.Append((char)(index % 251));
        }

        var written = await RespondAsync(new BigBodyRoute(), "GET /big HTTP/1.1\r\n\r\nGET /big HTTP/1.1\r\n\r\n");

        var answer = $"HTTP/1.1 200 OK\r\n{Date}Content-Type: application/octet-stream\r\nContent-Length: 3000000\r\n\r\n{body}";
        Assert.AreEqual(answer + answer, written);
    }
}
