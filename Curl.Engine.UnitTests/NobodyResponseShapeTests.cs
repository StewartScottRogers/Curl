using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// What a <c>CURLOPT_NOBODY</c> HTTP transfer gives its callbacks for each response shape (BL-3216), as curl 8.22.0
/// measured with <c>-I --no-include -D</c>: the write callback gets nothing, the header callback gets every head
/// received (both hops of a followed redirect, and a 404's head even under <c>CURLOPT_FAILONERROR</c>), and no body
/// is read, whatever the head's Content-Length or Transfer-Encoding says.
/// </summary>
[TestClass]
public sealed class NobodyResponseShapeTests
{
    private const string RedirectHead = "HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 3\r\n\r\n";

    private const string OkHead = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n";

    private const string ChunkedHead = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n";

    private const string NoContentHead = "HTTP/1.1 204 No Content\r\n\r\n";

    private const string NotModifiedHead = "HTTP/1.1 304 Not Modified\r\nETag: \"e\"\r\n\r\n";

    private const string NotFoundHead = "HTTP/1.1 404 Not Found\r\nContent-Length: 9\r\n\r\n";

    [TestMethod]
    public async Task PerformAsync_NobodyGet_GivesTheHeaderCallbackTheHeadAndTheWriteCallbackNothing()
    {
        await AssertShapeAsync([OkHead], OkHead, 200, CurlCode.Ok);
    }

    [TestMethod]
    public async Task PerformAsync_NobodyFollowedRedirect_GivesTheHeaderCallbackBothHeadsAndTheWriteCallbackNothing()
    {
        await AssertShapeAsync([RedirectHead, OkHead], RedirectHead + OkHead, 200, CurlCode.Ok, followLocation: true);
    }

    [TestMethod]
    public async Task PerformAsync_NobodyChunked_GivesTheHeaderCallbackTheHeadAndTheWriteCallbackNothing()
    {
        await AssertShapeAsync([ChunkedHead], ChunkedHead, 200, CurlCode.Ok);
    }

    [TestMethod]
    public async Task PerformAsync_NobodyNoContent_GivesTheHeaderCallbackTheHeadAndTheWriteCallbackNothing()
    {
        await AssertShapeAsync([NoContentHead], NoContentHead, 204, CurlCode.Ok);
    }

    [TestMethod]
    public async Task PerformAsync_NobodyNotModified_GivesTheHeaderCallbackTheHeadAndTheWriteCallbackNothing()
    {
        await AssertShapeAsync([NotModifiedHead], NotModifiedHead, 304, CurlCode.Ok);
    }

    [TestMethod]
    [DataRow(false, CurlCode.Ok)]
    [DataRow(true, CurlCode.HttpReturnedError)]
    public async Task PerformAsync_NobodyNotFound_GivesTheHeaderCallbackTheHeadAndTheWriteCallbackNothing(bool failOnError, CurlCode expectedCode)
    {
        await AssertShapeAsync([NotFoundHead], NotFoundHead, 404, expectedCode, failOnError: failOnError);
    }

    private static async Task AssertShapeAsync(
        string[] replies,
        string expectedHeaders,
        long expectedResponseCode,
        CurlCode expectedCode,
        bool followLocation = false,
        bool failOnError = false)
    {
        ScriptedConnector connector = new(replies.Select(Encoding.ASCII.GetBytes));
        await using ManagedCurlEngine engine = new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
        await using IEasyTransfer easy = engine.CreateEasy();
        StringBuilder body = new();
        StringBuilder headers = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/a" },
            Behaviour = new EasyBehaviourOptions { FailOnError = failOnError },
            Http = new EasyHttpOptions { FollowLocation = followLocation ? FollowLocation.All : default },
            Transfer = new EasyTransferOptions { NoBody = true },
            Callbacks = new()
            {
                Write = data =>
                {
                    body.Append(Encoding.ASCII.GetString(data));
                    return data.Length;
                },
                Header = data =>
                {
                    headers.Append(Encoding.ASCII.GetString(data));
                    return data.Length;
                },
            },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(expectedCode, code);
        Assert.AreEqual(string.Empty, body.ToString());
        Assert.AreEqual(expectedHeaders, headers.ToString());
        Assert.AreEqual(expectedResponseCode, easy.Info.ResponseCode);
    }
}
