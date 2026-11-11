using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Redis.Client;

/// <summary>
/// Checks that <c>-T</c> on <c>redis://</c> sends the uploaded file's bytes as the command's last argument, as
/// <c>-d</c> does (BL-4953), and that <c>-d</c> wins when both are given.
/// </summary>
[TestClass]
public sealed class RedisUploadTests
{
    private const string HelloReply = "%1\r\n$5\r\nproto\r\n:3\r\n";

    [TestMethod]
    public async Task ExecuteAsync_Upload_SendsTheFileAsTheLastArgument()
    {
        ScriptedRedisConnection connection = ScriptedRedisConnection.Replaying(HelloReply, "+OK\r\n");

        TransferResult result = await ExecuteAsync(connection, postData: null, upload: "filebody");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("*3\r\n$3\r\nSET\r\n$2\r\nkk\r\n$8\r\nfilebody\r\n", connection.WrittenText.ElementAt(1));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadAndPostData_SendsThePostData()
    {
        ScriptedRedisConnection connection = ScriptedRedisConnection.Replaying(HelloReply, "+OK\r\n");

        await ExecuteAsync(connection, postData: "dd", upload: "filebody");

        Assert.AreEqual("*3\r\n$3\r\nSET\r\n$2\r\nkk\r\n$2\r\ndd\r\n", connection.WrittenText.ElementAt(1));
    }

    private static async Task<TransferResult> ExecuteAsync(ScriptedRedisConnection connection, string? postData, string upload)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("redis://cache/SET/kk"),
            Output = new MemoryStream(),
            PostData = postData is null ? (ReadOnlyMemory<byte>?)null : Encoding.UTF8.GetBytes(postData),
            Upload = new MemoryStream(Encoding.UTF8.GetBytes(upload)),
        };
        return await new RedisProtocolHandler(new ScriptedRedisConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);
    }
}
