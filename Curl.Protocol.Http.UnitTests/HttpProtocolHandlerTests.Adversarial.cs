using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Adversarial black-box attacks on <see cref="HttpProtocolHandler" /> through its public surface
/// only (BL-1510, by <c>Documentation/Wiki/Adversarial-Testing.md</c>): status lines at and past
/// their boundaries, malformed and conflicting framing, responses cut short, CRLF carried by
/// <c>-H</c> and <c>-A</c>, and the same handler driven repeatedly and concurrently. Every
/// expected exit code, error text and body was measured 2026-10-07 with
/// <c>Record-CurlExchange.ps1</c> against curl 8.21.0 (Schannel) with <c>-sS -o</c>.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 599 X\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Status 599")]
    [DataRow("HTTP/1.1 600 X\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Status 600")]
    [DataRow("HTTP/1.1 999 X\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Status 999")]
    [DataRow("HTTP/1.1 200\r\nContent-Length: 2\r\n\r\nok", DisplayName = "No reason phrase")]
    [DataRow("HTTP/1.0 200 OK\r\n\r\nok", DisplayName = "HTTP/1.0 read to close")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Length: 2\r\n\r\nok", DisplayName = "The same Content-Length twice")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 100\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\n\r\n", DisplayName = "Content-Length beside chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nok", DisplayName = "Content-Length past long.MaxValue")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2;a=b\r\nok\r\n0\r\n\r\n", DisplayName = "Chunk extension")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\nX-T: 1\r\n\r\n", DisplayName = "Trailer")]
    [DataRow("HTTP/1.1 200 OK\r\nX-A: a\r\n\tb\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Obsolete line folding")]
    [DataRow("HTTP/1.1 200 OK\nContent-Length: 2\n\nok", DisplayName = "Bare LF line ends")]
    public async Task ExecuteAsync_AdversarialResponseCurlAccepts_DeliversTheBody(string response)
    {
        await AssertAdversarialResponseAsync(response, CurlExitCode.Ok, null, "ok");
    }

    [TestMethod]
    public async Task ExecuteAsync_ThirtyInterim100ResponsesBeforeTheFinalOne_DeliversTheBody()
    {
        string response = string.Concat(Enumerable.Repeat("HTTP/1.1 100 Continue\r\n\r\n", 30)) + "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

        await AssertAdversarialResponseAsync(response, CurlExitCode.Ok, null, "ok");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 99 X\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Two-digit status 99")]
    [DataRow("HTTP/1.1 20 X\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Two-digit status 20")]
    [DataRow("HTTP/1.1 2x0 X\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Letter in the status code")]
    [DataRow("HTTP/1.2 200 OK\r\nContent-Length: 2\r\n\r\nok", DisplayName = "HTTP/1.2")]
    public async Task ExecuteAsync_StatusLineOutsideHttp1_FailsWithUnsupportedSubversion(string response)
    {
        await AssertAdversarialResponseAsync(response, CurlExitCode.UnsupportedProtocol, "Unsupported HTTP/1 subversion in response", "");
    }

    [TestMethod]
    public async Task ExecuteAsync_FourDigitStatusCode_FailsWithInvalidStatusLine()
    {
        await AssertAdversarialResponseAsync("HTTP/1.1 1000 X\r\nContent-Length: 2\r\n\r\nok", CurlExitCode.WeirdServerReply, "Invalid status line", "");
    }

    [TestMethod]
    public async Task ExecuteAsync_GarbageStatusLine_FailsAsHttp09NotAllowed()
    {
        await AssertAdversarialResponseAsync("GARBAGE\r\n\r\n", CurlExitCode.UnsupportedProtocol, "Received HTTP/0.9 when not allowed", "");
    }

    [TestMethod]
    public async Task ExecuteAsync_OnlyAnInterim100ThenClose_FailsWithEmptyReply()
    {
        await AssertAdversarialResponseAsync("HTTP/1.1 100 Continue\r\n\r\n", CurlExitCode.GotNothing, "Empty reply from server", "");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Length: 3\r\n\r\nok!", DisplayName = "Two different Content-Length values")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: -1\r\n\r\nok", DisplayName = "Negative Content-Length")]
    public async Task ExecuteAsync_InvalidContentLength_FailsWithWeirdServerReply(string response)
    {
        await AssertAdversarialResponseAsync(response, CurlExitCode.WeirdServerReply, "Invalid Content-Length: value", "");
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nNoColonHere\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Header line with no colon")]
    [DataRow("HTTP/1.1 200 OK\r\n b\r\nContent-Length: 2\r\n\r\nok", DisplayName = "Continuation line before any header")]
    public async Task ExecuteAsync_HeaderWithoutColon_FailsWithWeirdServerReply(string response)
    {
        await AssertAdversarialResponseAsync(response, CurlExitCode.WeirdServerReply, "Header without colon", "");
    }

    [TestMethod]
    [DataRow("10000000000000000", "chunk hex-length longer than 16", DisplayName = "Seventeen hex digits")]
    [DataRow("FFFFFFFFFFFFFFFF", "invalid chunk size: 'FFFFFFFFFFFFFFFF'", DisplayName = "Sixteen hex digits past long.MaxValue")]
    [DataRow("zz", "chunk hex-length char not a hex digit: 0x7a", DisplayName = "Not a hex digit")]
    public async Task ExecuteAsync_ChunkSizeCurlRefuses_FailsWithReceiveError(string chunkSize, string message)
    {
        string response = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n" + chunkSize + "\r\nok\r\n0\r\n\r\n";

        await AssertAdversarialResponseAsync(response, CurlExitCode.RecvError, message, "");
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyShorterThanContentLength_FailsWithPartialFileAfterDeliveringWhatCame()
    {
        await AssertAdversarialResponseAsync("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nok", CurlExitCode.PartialFile, "end of response with 8 bytes missing", "ok");
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkCutShort_FailsWithPartialFileAfterDeliveringWhatCame()
    {
        await AssertAdversarialResponseAsync(
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nok",
            CurlExitCode.PartialFile,
            "transfer closed with outstanding read data remaining",
            "ok");
    }

    [TestMethod]
    [DataRow("X-A: a\r\nEvil: 1", "\r\nX-A: a\r\nEvil: 1\r\n", DisplayName = "CRLF in -H")]
    [DataRow("X-B: a\nEvil: 2", "\r\nX-B: a\nEvil: 2\r\n", DisplayName = "Bare LF in -H")]
    public async Task ExecuteAsync_LineBreakInACustomHeader_SendsItVerbatimAsCurlDoes(string header, string expected)
    {
        await AssertAdversarialRequestAsync(new HttpRequestOptions { Headers = [header] }, expected);
    }

    [TestMethod]
    public async Task ExecuteAsync_CrlfInTheUserAgent_SendsItVerbatimAsCurlDoes()
    {
        await AssertAdversarialRequestAsync(new HttpRequestOptions { UserAgent = "ua\r\nEvil: 1" }, "\r\nUser-Agent: ua\r\nEvil: 1\r\nAccept: */*\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_OneHandlerAlternatingRefusedAndValidResponses_EachTransferStandsAlone()
    {
        string[] responses =
        [
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Length: 3\r\n\r\nok!",
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok",
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n",
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nok\r\n0\r\n\r\n",
            "GARBAGE\r\n\r\n",
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok",
        ];
        CurlExitCode[] expected =
        [
            CurlExitCode.WeirdServerReply, CurlExitCode.Ok, CurlExitCode.RecvError, CurlExitCode.Ok, CurlExitCode.UnsupportedProtocol, CurlExitCode.Ok,
        ];
        ScriptedConnection[] connections = [.. responses.Select(response => Connection(response, 7))];
        HttpProtocolHandler handler = Handler(QueueConnector.For(connections));
        Diagnostics.Arrange("responses", string.Join(" | ", responses.Select(OneLine)));

        List<CurlExitCode> exits = [];
        List<string> bodies = [];
        foreach (string _ in responses)
        {
            MemoryStream output = new();
            TransferResult result = await handler.ExecuteAsync(Context("http://example.com/", output));
            WriteResult(result);
            exits.Add(result.ExitCode);
            bodies.Add(Latin1(output.ToArray()));
        }

        Diagnostics.Assert("exit codes", string.Join(",", expected), string.Join(",", exits));
        CollectionAssert.AreEqual(expected, exits);
        CollectionAssert.AreEqual(new[] { "", "ok", "", "ok", "", "ok" }, bodies);
    }

    [TestMethod]
    public async Task ExecuteAsync_OneHandlerSixteenConcurrentTransfers_EachGetsExactlyOneWholeBody()
    {
        string[] bodies = [.. Enumerable.Range(0, 16).Select(index => $"body-{index:D2}")];
        ConcurrentConnector connector = new(bodies.Select(body =>
            (IConnection)Connection($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}", 3)));
        HttpProtocolHandler handler = new(connector, new SilentAuthenticator());
        Diagnostics.Arrange("transfers", bodies.Length);

        MemoryStream[] outputs = [.. bodies.Select(_ => new MemoryStream())];
        TransferResult[] results = await Task.WhenAll(outputs.Select(output =>
            Task.Run(async () => await handler.ExecuteAsync(Context("http://example.com/", output)))));

        string[] received = [.. outputs.Select(output => Latin1(output.ToArray())).Order(StringComparer.Ordinal)];
        Diagnostics.Assert("bodies", string.Join(",", bodies), string.Join(",", received));
        Assert.IsTrue(results.All(result => result.ExitCode == CurlExitCode.Ok), string.Join(",", results.Select(result => result.ErrorMessage)));
        CollectionAssert.AreEqual(bodies, received);
    }

    private async Task AssertAdversarialResponseAsync(string response, CurlExitCode exitCode, string? message, string body)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", OneLine(response));

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize))).ExecuteAsync(Context("http://example.com/", output));

            WriteResult(result);
            Diagnostics.Assert("exit code", exitCode, result.ExitCode);
            Diagnostics.Assert("error text", message ?? "(none)", result.ErrorMessage ?? "(none)");
            Diagnostics.Assert("body", body, Latin1(output.ToArray()));
            Assert.AreEqual(exitCode, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(body, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    private async Task AssertAdversarialRequestAsync(HttpRequestOptions options, string expected)
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536);
        Diagnostics.Arrange("expected request fragment", OneLine(expected));

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), Http = options });

        WriteResult(result);
        string request = Latin1(connection.Written);
        Diagnostics.Assert("request", OneLine(expected), OneLine(request));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsTrue(request.Contains(expected, StringComparison.Ordinal), OneLine(request));
    }

    /// <summary>Hands out scripted connections to connects arriving from any thread.</summary>
    private sealed class ConcurrentConnector(IEnumerable<IConnection> connections) : IConnector
    {
        private readonly ConcurrentQueue<IConnection> pending = new(connections);

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            pending.TryDequeue(out IConnection? connection)
                ? ValueTask.FromResult(ConnectResult.Connected(connection))
                : throw new InvalidOperationException("No scripted connection left.");
    }
}
