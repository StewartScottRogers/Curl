using Curl.Extended.Protocol.Amqp0.Client;
using Curl.Extended.Protocol.Amqp0.Methods;
using Curl.Extended.Protocol.Amqp0.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp0;

[TestClass]
public sealed class Amqp0UploadTests
{
    [TestMethod]
    public async Task ExecuteAsync_Upload_PublishesTheFileAndNeverGets()
    {
        ScriptedAmqp0Connection connection = new([.. Opened(), .. Closes()]);
        MemoryStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse("amqp://broker/v/q"), Output = output, Upload = new MemoryStream("filebody"u8.ToArray()) };

        TransferResult result = await new Amqp0ProtocolHandler(new ScriptedAmqp0Connector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, output.Length);
        List<Amqp0Frame> written = [.. connection.Writes.Skip(1).SelectMany(Frames)];
        List<Amqp0Frame> published = Frames(connection.Writes[6]);
        Assert.AreEqual(new Amqp0BasicPublish(string.Empty, "q"), Amqp0MethodReader.Read(published[0]));
        Assert.AreEqual(8UL, Amqp0ContentHeader.Read(published[1].Payload).BodySize);
        CollectionAssert.AreEqual("filebody"u8.ToArray(), published[2].Payload.ToArray());
        Assert.IsFalse(written.Exists(frame => frame.Type == Amqp0FrameType.Method && Amqp0MethodReader.Read(frame) is Amqp0BasicGet));
    }

    [TestMethod]
    public async Task ExecuteAsync_DataAndUpload_PublishesTheData()
    {
        ScriptedAmqp0Connection connection = new([.. Opened(), .. Closes()]);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("amqp://broker/v/q"),
            Output = new MemoryStream(),
            PostData = "data"u8.ToArray(),
            Upload = new MemoryStream("filebody"u8.ToArray()),
        };

        TransferResult result = await new Amqp0ProtocolHandler(new ScriptedAmqp0Connector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual("data"u8.ToArray(), Frames(connection.Writes[6])[2].Payload.ToArray());
    }

    private static byte[] Opened() =>
    [
        .. Frame(new Amqp0ConnectionStart(0, 9, Amqp0FieldTable.Empty, "PLAIN AMQPLAIN", "en_US")),
        .. Frame(new Amqp0ConnectionTune(0, 0, 60)),
        .. Frame(new Amqp0ConnectionOpenOk()),
        .. Frame(new Amqp0ChannelOpenOk(), 1),
        .. Frame(new Amqp0QueueDeclareOk("q", 0, 0), 1),
    ];

    private static byte[] Closes() => [.. Frame(new Amqp0ChannelCloseOk(), 1), .. Frame(new Amqp0ConnectionCloseOk())];

    private static byte[] Frame(Amqp0Method method, ushort channel = 0) => method.ToFrame(channel).ToBytes();

    private static List<Amqp0Frame> Frames(byte[] written)
    {
        List<Amqp0Frame> frames = [];
        int offset = 0;
        while (Amqp0Frame.TryRead(written.AsSpan(offset), 0, out Amqp0Frame? frame, out int consumed))
        {
            frames.Add(frame!);
            offset += consumed;
        }

        return frames;
    }
}
