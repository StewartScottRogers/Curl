using System.Text;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// curl 8.22.0's <c>-v</c> lines, order of reading and sending, and end of connection for a final
/// response that arrives while the body waits for <c>100 Continue</c> and leads to no resend
/// (measured with <c>Record-CurlExchange.ps1 -HoldOpenMilliseconds 3000</c>, BL-5960 Notes).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private static readonly TimeSpan HeldWait = TimeSpan.FromSeconds(5);

    [TestMethod]
    [DataRow(
        "500 X\r\nContent-Length: 4\r\n",
        false,
        new[] { "< Content-Length: 4", "* HTTP error before end of send, stop sending", "* abort upload", "< ", "* shutting down connection #0" },
        DisplayName = "500")]
    [DataRow(
        "401 X\r\nContent-Length: 4\r\n",
        false,
        new[] { "< Content-Length: 4", "* HTTP error before end of send, stop sending", "* abort upload", "< ", "* shutting down connection #0" },
        DisplayName = "401 with nothing to answer it")]
    [DataRow(
        "301 X\r\nContent-Length: 4\r\nLocation: /n\r\n",
        true,
        new[] { "< Content-Length: 4", "< Location: /n", "* HTTP error before end of send, stop sending", "* abort upload", "< ", "* shutting down connection #0" },
        DisplayName = "301 with -L")]
    [DataRow(
        "417 X\r\nContent-Length: 4\r\nConnection: close\r\n",
        false,
        new[] { "< Content-Length: 4", "< Connection: close", "< ", "* we are done reading and this is set to close, stop send", "* abort upload", "* shutting down connection #0" },
        DisplayName = "closing 417")]
    [DataRow(
        "500 X\r\nContent-Length: 4\r\nConnection: close\r\n",
        false,
        new[] { "< Content-Length: 4", "< Connection: close", "< ", "* we are done reading and this is set to close, stop send", "* abort upload", "* shutting down connection #0" },
        DisplayName = "closing 500")]
    [DataRow(
        "200 OK\r\nContent-Length: 4\r\nConnection: close\r\n",
        false,
        new[] { "< Content-Length: 4", "< Connection: close", "< ", "* we are done reading and this is set to close, stop send", "* abort upload", "* shutting down connection #0" },
        DisplayName = "closing 200")]
    public async Task ExecuteAsync_FinalResponseDuringContinueWaitThatStopsTheUpload_EndsAsCurlDoes(
        string head, bool followsRedirects, string[] expected)
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 " + head + "\r\nnope");
        RecordingTransferEvents events = new();
        HttpRequestOptions options = HeldBodyOptions() with { FollowRedirects = followsRedirects };
        Diagnostics.Arrange("response head", head);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ExpectEventsContext(options, events));

        WriteResult(result);
        string[] actual = VerboseLinesAfterStatusLine(events);
        Diagnostics.Diff("-v lines after the status line", string.Join('\n', expected), string.Join('\n', actual));
        CollectionAssert.AreEqual(expected, actual);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOnA417DuringContinueWait_WritesNoUploadLine()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 417 X\r\nContent-Length: 4\r\n\r\nnope");
        RecordingTransferEvents events = new();
        HttpRequestOptions options = HeldBodyOptions() with { Fail = HttpFailMode.Fail };
        Diagnostics.Arrange("options", "-f");

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ExpectEventsContext(options, events));

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.DoesNotContain(line => line.Contains("upload", StringComparison.Ordinal), events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_Kept200DuringContinueWait_ReadsTheResponseThenSendsTheBodyWhenTheWaitRunsOut()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        MemoryStream output = new();
        int waitsEnded = 0;
        HttpProtocolHandler handler = new(QueueConnector.For(connection), new SilentAuthenticator()) { ContinueWaitEnds = () => waitsEnded++ };
        Diagnostics.Arrange("response", "200 with Content-Length: 2, nothing before it");

        Task<TransferResult> transfer = handler
            .ExecuteAsync(TimedContext(events, time, output))
            .AsTask();
        string deliveredBeforeTheWaitRanOut = Encoding.Latin1.GetString(output.ToArray());
        bool completedBeforeTheWaitRanOut = transfer.IsCompleted;
        int waitsEndedBeforeTheWaitRanOut = waitsEnded;
        time.Advance(HeldWait);
        TransferResult result = await transfer;

        WriteResult(result);
        string[] expected =
        [
            "< Content-Length: 2",
            "< ",
            "* Done waiting for 100-continue",
            "* upload completely sent off: 3 bytes",
            "* Connection #0 to host 127.0.0.1:18260 left intact",
        ];
        string[] actual = VerboseLinesAfterStatusLine(events);
        Diagnostics.Diff("-v lines after the status line", string.Join('\n', expected), string.Join('\n', actual));
        CollectionAssert.AreEqual(expected, actual);
        Assert.IsFalse(completedBeforeTheWaitRanOut);
        Assert.AreEqual("ok", deliveredBeforeTheWaitRanOut);
        Assert.AreEqual(0, waitsEndedBeforeTheWaitRanOut);
        Assert.AreEqual(1, waitsEnded);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.Report!.UploadSize);
    }

    [TestMethod]
    [DataRow("200 OK", DisplayName = "200 kept open")]
    [DataRow("500 X\r\nConnection: close", DisplayName = "500 that closes")]
    public async Task ExecuteAsync_ResponseBodyOutlastingTheContinueWait_SendsTheBodyWhileReadingIt(string status)
    {
        AnswersAfterTheBodyConnection connection = new($"HTTP/1.1 {status}\r\nContent-Length: 2\r\n\r\n", "ok");
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        MemoryStream output = new();
        Diagnostics.Arrange("response", "head at once, body only once the request body has arrived");

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(TimedContext(events, time, output))
            .AsTask();
        time.Advance(HeldWait);
        TransferResult result = await transfer;

        WriteResult(result);
        CollectionAssert.IsSubsetOf(
            new[] { "Done waiting for 100-continue", "upload completely sent off: 3 bytes" }, events.Info.ToArray());
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_200DuringTheSendAfterTheContinueWaitThenSendFails_DeliversTheResponseThenFailsWithSendError()
    {
        AnswersThenFailsTheBodyConnection connection = new("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        MemoryStream output = new();
        Diagnostics.Arrange("response", "200 as the request body is written, which then fails");

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(TimedContext(new RecordingTransferEvents(), time, output))
            .AsTask();
        time.Advance(HeldWait);
        TransferResult result = await transfer;

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("ok", Encoding.Latin1.GetString(output.ToArray()));
    }

    /// <summary>A server that answers only as the second write, the request body, arrives, and fails that write.</summary>
    private sealed class AnswersThenFailsTheBodyConnection(string response) : IConnection
    {
        private readonly TaskCompletionSource answered = new();

        private int writes;

        private bool read;

        public bool IsSecure => false;

        public System.Net.EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await answered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (read)
            {
                return 0;
            }

            read = true;
            return Encoding.Latin1.GetBytes(response, buffer.Span);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (++writes == 1)
            {
                return ValueTask.CompletedTask;
            }

            answered.SetResult();
            throw new IOException("Connection was aborted", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionAborted));
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static TransferContext TimedContext(ITransferEvents events, TimeProvider time, Stream output) =>
        new()
        {
            Url = CurlUrl.Parse(ExpectUrl),
            Output = output,
            Http = HeldBodyOptions(),
            Events = events,
            TimeProvider = time,
        };

    private static HttpRequestOptions HeldBodyOptions() =>
        new() { Body = new BytesBody("x=1"u8.ToArray(), "application/x-www-form-urlencoded"), ContinueWait = HeldWait, Headers = ["Expect: 100-continue"] };

    private static string[] VerboseLinesAfterStatusLine(RecordingTransferEvents events)
    {
        string[] lines = [.. events.Events.Select(line => line.TrimEnd('\r', '\n'))];
        int status = Array.FindIndex(lines, line => line.StartsWith("< HTTP/", StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, status, string.Join(" | ", lines));
        return [.. lines.Skip(status + 1).Where(line => line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("< ", StringComparison.Ordinal))];
    }

    /// <summary>
    /// A server that sends the head at once, and the response body only once it has received a
    /// second write: the request body, after the request head.
    /// </summary>
    private sealed class AnswersAfterTheBodyConnection(string head, string body) : IConnection
    {
        private readonly TaskCompletionSource bodyArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int writes;

        private int reads;

        public bool IsSecure => false;

        public System.Net.EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (reads++ == 0)
            {
                return Encoding.Latin1.GetBytes(head, buffer.Span);
            }

            await bodyArrived.Task.WaitAsync(cancellationToken);
            return Encoding.Latin1.GetBytes(body, buffer.Span);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (++writes == 2)
            {
                bodyArrived.SetResult();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
