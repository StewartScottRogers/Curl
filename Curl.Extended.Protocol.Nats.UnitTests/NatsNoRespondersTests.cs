using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>Checks that a <c>?request</c> nobody answers fails at once with "no responders" (BL-5409).</summary>
[TestClass]
public sealed class NatsNoRespondersTests
{
    private const string Info = "INFO {\"server_id\":\"x\",\"max_payload\":1024}\r\n";

    [TestMethod]
    public async Task ExecuteAsync_ARequest_AsksForHeadersAndNoResponders()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "MSG _INBOX.t 1 1\r\nx\r\n");

        await ExecuteAsync(connection, "nats://example.com/svc?request", "ping", new MemoryStream());

        StringAssert.Contains(connection.Sent, "\"protocol\":1,\"headers\":true,\"no_responders\":true}");
    }

    [TestMethod]
    public async Task ExecuteAsync_APublishOrSubscribe_DoesNotAskForHeaders()
    {
        ScriptedConnection publish = ScriptedConnection.Of(Info, "PONG\r\n");
        ScriptedConnection subscribe = ScriptedConnection.Of(Info);

        await ExecuteAsync(publish, "nats://example.com/svc", "ping", new MemoryStream());
        await ExecuteAsync(subscribe, "nats://example.com/svc", null, new MemoryStream());

        Assert.DoesNotContain("no_responders", publish.Sent);
        Assert.DoesNotContain("no_responders", subscribe.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ARequestAnsweredWith503_FailsWithExit56AndNamesNoResponders()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "PONG\r\n", "HMSG _INBOX.t 1 16 16\r\nNATS/1.0 503\r\n\r\n\r\n");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "nats://example.com/nobody?request", "ping", output);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.Contains("no responders", result.ErrorMessage ?? string.Empty);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ARequestAnsweredWithAHeaderMessage_WritesOnlyThePayload()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "HMSG _INBOX.t 1 _R_ 18 22\r\nNATS/1.0\r\nK: v\r\n\r\npong\r\n");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "nats://example.com/svc?request", "ping", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("pong", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow("HMSG a 1 5", "unexpected line")]
    [DataRow("HMSG a 1 9 5", "unexpected line")]
    [DataRow("HMSG a 1 x 5", "unexpected line")]
    [DataRow("HMSG", "unexpected line")]
    public async Task ExecuteAsync_AMalformedHeaderMessage_FailsWithExit8(string line, string text)
    {
        TransferResult result = await ExecuteAsync(ScriptedConnection.Of(Info, line + "\r\n"), "nats://example.com/svc?request", "ping", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.Contains(text, result.ErrorMessage ?? string.Empty);
    }

    [TestMethod]
    public void Parse_ABlankLine_Throws() =>
        Assert.ThrowsExactly<NatsException>(() => NatsMessageLine.Parse("  ", 10));

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedConnection connection, string url, string? data, Stream output) =>
        new NatsProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), () => "_INBOX.t").ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(url), Output = output, PostData = data is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(data) });
}
