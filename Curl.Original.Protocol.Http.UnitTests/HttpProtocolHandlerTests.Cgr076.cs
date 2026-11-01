using System.Net;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins CGR-076 (BL-3605): a hop that a redirect to another host withheld the <c>-H</c>
/// <c>Authorization</c> value from writes no <c>Server auth using Basic</c> line for the
/// Location's <c>x:y@</c>, as curl 8.22.0's <c>-v</c> writes none before <c>GET /ok</c>
/// (measured, BL-3605 Notes).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_AuthorizationHeaderWithheldVerbose_WritesNoServerAuthUsingLine()
    {
        HttpRequestOptions options = new() { FollowedToAnotherHost = true, AuthorizationHeaderWithheld = true };

        List<string> events = await AuthUsingEventsAsync(options, new NetworkCredential("x", "y"), OkHead + "ok");

        string[] expected = ["* using HTTP/1.x", AuthUsingRequestLine];
        WriteExpectedLines("auth using lines", expected, events);
        CollectionAssert.AreEqual(expected, events);
    }
}
