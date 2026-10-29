using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Redis.Client;

/// <summary>
/// Checks the client exchange against RESP3's "Sending commands to a Redis server" and "The HELLO command and
/// connection handshake", and the redis URI scheme's host, port, user information and <c>db-number</c>.
/// </summary>
[TestClass]
public sealed class RedisProtocolHandlerTests
{
    private const string HelloReply = "%1\r\n$5\r\nproto\r\n:3\r\n";

    private const string Hello = "*2\r\n$5\r\nHELLO\r\n$1\r\n3\r\n";

    [TestMethod]
    public async Task ExecuteAsync_Command_SendsHelloThenTheCommandAndWritesItsReply()
    {
        ScriptedRedisConnection connection = ScriptedRedisConnection.Replaying(HelloReply, "$5\r\nvalue\r\n");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "redis://cache/GET/my%20key", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(6L, result.BytesTransferred);
        Assert.AreEqual("value\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(Hello + "|*2\r\n$3\r\nGET\r\n$6\r\nmy key\r\n", string.Join("|", connection.WrittenText));
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_DatabaseInThePath_SelectsItBeforeTheCommand()
    {
        ScriptedRedisConnection connection = ScriptedRedisConnection.Replaying(HelloReply, "+OK\r\n", ":1\r\n");

        TransferResult result = await ExecuteAsync(connection, "redis://cache/3/INCR/n", new MemoryStream());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("*2\r\n$6\r\nSELECT\r\n$1\r\n3\r\n", connection.WrittenText.ElementAt(1));
    }

    [TestMethod]
    public async Task ExecuteAsync_Body_IsTheLastArgument()
    {
        ScriptedRedisConnection connection = ScriptedRedisConnection.Replaying(HelloReply, "+OK\r\n");

        await ExecuteAsync(connection, "redis://cache/SET/k", new MemoryStream(), postData: "a b");

        Assert.AreEqual("*3\r\n$3\r\nSET\r\n$1\r\nk\r\n$3\r\na b\r\n", connection.WrittenText.ElementAt(1));
    }

    [TestMethod]
    [DataRow("alice", "*5\r\n$5\r\nHELLO\r\n$1\r\n3\r\n$4\r\nAUTH\r\n$5\r\nalice\r\n$2\r\npw\r\n")]
    [DataRow("", "*5\r\n$5\r\nHELLO\r\n$1\r\n3\r\n$4\r\nAUTH\r\n$7\r\ndefault\r\n$2\r\npw\r\n")]
    public async Task ExecuteAsync_Credentials_AuthenticateInHello(string user, string hello)
    {
        ScriptedRedisConnection connection = ScriptedRedisConnection.Replaying(HelloReply, "+PONG\r\n");

        await ExecuteAsync(connection, "redis://cache/PING", new MemoryStream(), new NetworkCredential(user, "pw"));

        Assert.AreEqual(hello, connection.WrittenText.First());
    }

    [TestMethod]
    [DataRow("-WRONGPASS invalid username-password pair\r\n", true, CurlExitCode.LoginDenied)]
    [DataRow("-NOAUTH HELLO must be called with the client already authenticated\r\n", false, CurlExitCode.LoginDenied)]
    [DataRow("-ERR unknown command 'HELLO'\r\n", false, CurlExitCode.WeirdServerReply)]
    public async Task ExecuteAsync_HelloRefused_EndsWithLoginDeniedOnlyForAuthentication(string reply, bool withCredentials, CurlExitCode exitCode)
    {
        ScriptedRedisConnection connection = ScriptedRedisConnection.Replaying(reply);

        TransferResult result = await ExecuteAsync(connection, "redis://cache/PING", new MemoryStream(), withCredentials ? new NetworkCredential("u", "p") : null);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual($"Redis server answered HELLO with error: {reply.TrimEnd()[1..]}", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("redis://cache/9/GET/k", "-ERR DB index is out of range\r\n", "Redis server answered SELECT with error: ERR DB index is out of range")]
    [DataRow("redis://cache/LPOP/k", "-WRONGTYPE Operation against a key holding the wrong kind of value\r\n", "Redis server answered LPOP with error: WRONGTYPE Operation against a key holding the wrong kind of value")]
    [DataRow("redis://cache/GET/k", "!3\r\nERR\r\n", "Redis server answered GET with error: ERR")]
    public async Task ExecuteAsync_ErrorReply_EndsWithWeirdServerReplyAndWritesNothing(string url, string reply, string message)
    {
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(ScriptedRedisConnection.Replaying(HelloReply, reply), url, output);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_MalformedReply_EndsWithWeirdServerReply()
    {
        TransferResult result = await ExecuteAsync(ScriptedRedisConnection.Replaying(HelloReply, "?\r\n"), "redis://cache/GET/k", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Malformed RESP3 reply: unknown type", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("", false)]
    [DataRow("%1\r\n", false)]
    [DataRow("", true)]
    public async Task ExecuteAsync_ConnectionEndsOrFailsWhereAReplyIsDue_EndsWithRecvError(string replayed, bool failsReads)
    {
        ScriptedRedisConnection connection = new([Encoding.UTF8.GetBytes(replayed)]) { FailsReads = failsReads };

        TransferResult result = await ExecuteAsync(connection, "redis://cache/PING", new MemoryStream());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(RedisClientMessages.Disconnected, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteToServerFails_EndsWithSendError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedRedisConnection() { FailsWrites = true }, "redis://cache/PING", new MemoryStream());

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending Redis HELLO command", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteToOutputFails_EndsWithWriteError()
    {
        TransferResult result = await ExecuteAsync(ScriptedRedisConnection.Replaying(HelloReply, "+PONG\r\n"), "redis://cache/PING", new UnwritableOutputStream());

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 5 returned 0", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("redis://cache")]
    [DataRow("redis://cache/")]
    [DataRow("redis://cache/0")]
    public async Task ExecuteAsync_NoCommandInTheUrl_EndsWithUrlMalformatBeforeConnecting(string url)
    {
        ScriptedRedisConnector connector = new(ConnectResult.Refused("refused"));

        TransferResult result = await new RedisProtocolHandler(connector).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(RedisClientMessages.NoCommand, result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    [DataRow("redis://cache/PING", 6379, false)]
    [DataRow("rediss://cache/PING", 6379, true)]
    [DataRow("REDISS://cache:7000/PING", 7000, true)]
    public async Task ExecuteAsync_Url_ConnectsToItsPortWithTlsForRediss(string url, int port, bool useTls)
    {
        ScriptedRedisConnector connector = new(ConnectResult.Refused("refused"));

        TransferResult result = await new RedisProtocolHandler(connector).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("cache", connector.Target!.Host);
        Assert.AreEqual(port, connector.Target.Port);
        Assert.AreEqual(useTls, connector.Target.UseTls);
    }

    [TestMethod]
    public void SupportedSchemes_AreRedisAndRediss() =>
        CollectionAssert.AreEqual(new[] { "redis", "rediss" }, new RedisProtocolHandler(new ScriptedRedisConnector(ConnectResult.Refused("x"))).SupportedSchemes.ToArray());

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new RedisProtocolHandler(new ScriptedRedisConnector(ConnectResult.Refused("x"))).ExecuteAsync(null!));

    private static async Task<TransferResult> ExecuteAsync(ScriptedRedisConnection connection, string url, Stream output, NetworkCredential? credentials = null, string? postData = null)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Credentials = credentials,
            PostData = postData is null ? (ReadOnlyMemory<byte>?)null : Encoding.UTF8.GetBytes(postData),
        };
        return await new RedisProtocolHandler(new ScriptedRedisConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);
    }
}
