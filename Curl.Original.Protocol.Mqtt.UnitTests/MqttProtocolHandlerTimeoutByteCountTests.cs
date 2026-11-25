using System.Globalization;
using System.Net;
using Curl.Original.Protocol.Mqtt.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Mqtt;

/// <summary>
/// Pins the two numbers of the <c>-m</c> timeout message after PUBLISH packets (STD-214,
/// BL-5895). curl 8.22.0 resets <c>req.bytecount</c> and <c>req.size</c> for each PUBLISH, so
/// <c>with N out of M bytes received</c> counts the latest PUBLISH any of whose body arrived,
/// while <c>%{size_download}</c> counts every body. Measured on the Windows build with
/// <c>Record-CurlExchange.ps1 -Script</c> and <c>-m 1</c>: bodies of 20 and 13 bytes print
/// <c>13 out of 13</c>; 20 bytes and then 5 of 13 print <c>5 out of 13</c>; 20 bytes and then
/// the header <c>30 0d</c> alone print <c>20 out of 20</c>. The counts are platform-neutral.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerTimeoutByteCountTests
{
    /// <summary>A PUBLISH to topic <c>t</c> with an 8-byte body.</summary>
    private const string FirstPublish = "30 08 00 01 74 48 45 4C 4C 4F";

    [TestMethod]
    [DataRow("30 07 00 01 74 6D 6F 72 65", 7, 7, 15, DisplayName = "a whole second PUBLISH")]
    [DataRow("30 0D 00 01 74 6D 6F", 5, 13, 13, DisplayName = "part of a second PUBLISH")]
    [DataRow("30 0D", 8, 8, 8, DisplayName = "a second PUBLISH header with no body")]
    public async Task ExecuteAsync_MaxTimePassedAfterPublishes_TimeoutMessageCountsTheLatestPublishBody(
        string lastPacket,
        int messageBytes,
        int messageTotal,
        int downloadedBytes)
    {
        using CancellationTokenSource timeout = new();
        StallingConnection connection = new(
            new ScriptedConnection(Hex("20 02 00 00"), Hex("90 03 00 01 00"), Hex(FirstPublish), Hex(lastPacket)));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mqtt://h/t"),
            Output = new RecordingStream(),
            MaxTime = TimeSpan.FromSeconds(2),
            CancellationToken = timeout.Token,
        };

        // Every scripted read and write completes at once, so the handler has done all it can
        // with the script by the time this returns, waiting on the stalled read.
        ValueTask<TransferResult> transfer = new MqttProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context);
        await timeout.CancelAsync();
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        StringAssert.EndsWith(result.ErrorMessage, $"with {messageBytes} out of {messageTotal} bytes received");
        Assert.AreEqual(downloadedBytes, result.BytesTransferred);
    }

    private static byte[] Hex(string hex) =>
        [.. hex.Split(' ').Select(pair => byte.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture))];

    /// <summary>
    /// A peer that goes quiet once its script is sent: the next read stays pending, as a
    /// socket's does, until the transfer is cancelled.
    /// </summary>
    private sealed class StallingConnection(ScriptedConnection script) : IConnection
    {
        public bool IsSecure => script.IsSecure;

        public EndPoint? RemoteEndPoint => script.RemoteEndPoint;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int read = await script.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            return read;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            script.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => script.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => script.DisposeAsync();
    }
}
