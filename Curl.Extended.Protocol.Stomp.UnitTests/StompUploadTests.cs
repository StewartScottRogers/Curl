using System.Text;
using Curl.Core;
using Curl.Extended.Protocol.Stomp.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Stomp;

/// <summary>Checks that <c>-T</c> on stomp:// publishes the file as a SEND frame's body, as <c>-d</c> does.</summary>
[TestClass]
public sealed class StompUploadTests
{
    private const string Connected = "CONNECTED\nversion:1.2\n\n\0";

    private const string Receipt = "RECEIPT\nreceipt-id:disconnect\n\n\0";

    [TestMethod]
    public async Task ExecuteAsync_Upload_SendsTheFileAsTheSendBodyAndNeverSubscribes()
    {
        ScriptedStompConnection connection = ScriptedStompConnection.Replaying(Connected, Receipt);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("stomp://broker/queue/a"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.UTF8.GetBytes("filebody")),
        };

        TransferResult result = await new StompProtocolHandler(new ScriptedStompConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(
            "SEND\ndestination:/queue/a\ncontent-length:8\ncontent-type:application/octet-stream\n\nfilebody\0",
            connection.WrittenText.Single(frame => frame.StartsWith("SEND", StringComparison.Ordinal)));
        Assert.IsFalse(connection.WrittenText.Any(frame => frame.StartsWith("SUBSCRIBE", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_PostDataAndUpload_SendsThePostData()
    {
        ScriptedStompConnection connection = ScriptedStompConnection.Replaying(Connected, Receipt);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("stomp://broker/queue/a"),
            Output = new MemoryStream(),
            PostData = Encoding.UTF8.GetBytes("hi"),
            Upload = new MemoryStream(Encoding.UTF8.GetBytes("filebody")),
        };

        TransferResult result = await new StompProtocolHandler(new ScriptedStompConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.Contains(connection.WrittenText.Single(frame => frame.StartsWith("SEND", StringComparison.Ordinal)), "\n\nhi\0");
    }
}
