using Curl.Extension.Amqp0.Methods;
using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.UnitTests.Methods;

[TestClass]
public sealed class Amqp0MethodReaderTests
{
    private const string Q1 = "027131";

    private static readonly (Amqp0Method Method, string Hex)[] MethodsAndBytes =
    [
        (new Amqp0ConnectionStart(0, 9, new Amqp0FieldTable([new("product", Amqp0FieldValue.LongString("RabbitMQ"))]), "PLAIN AMQPLAIN", "en_US"),
            "000A000A0009" + "00000015" + "0770726F64756374" + "53" + "000000085261626269744D51"
            + "0000000E504C41494E20414D51504C41494E" + "00000005656E5F5553"),
        (new Amqp0ConnectionStartOk(Amqp0FieldTable.Empty, "PLAIN", "\0guest\0guest"u8.ToArray(), "en_US"),
            "000A000B" + "00000000" + "05504C41494E" + "0000000C006775657374006775657374" + "05656E5F5553"),
        (new Amqp0ConnectionTune(2047, 131072, 60), "000A001E" + "07FF" + "00020000" + "003C"),
        (new Amqp0ConnectionTuneOk(2047, 131072, 60), "000A001F" + "07FF" + "00020000" + "003C"),
        (new Amqp0ConnectionOpen("/"), "000A0028" + "012F" + "00" + "00"),
        (new Amqp0ConnectionOpenOk(), "000A0029" + "00"),
        (new Amqp0ConnectionClose(200, "Goodbye", 0, 0), "000A0032" + "00C8" + "07476F6F64627965" + "0000" + "0000"),
        (new Amqp0ConnectionCloseOk(), "000A0033"),
        (new Amqp0ChannelOpen(), "0014000A" + "00"),
        (new Amqp0ChannelOpenOk(), "0014000B" + "00000000"),
        (new Amqp0ChannelClose(406, "PRECONDITION_FAILED", 50, 10), "00140028" + "0196" + "13505245434F4E444954494F4E5F4641494C4544" + "0032" + "000A"),
        (new Amqp0ChannelCloseOk(), "00140029"),
        (new Amqp0QueueDeclare("q1") { Durable = true, AutoDelete = true }, "0032000A" + "0000" + Q1 + "0A" + "00000000"),
        (new Amqp0QueueDeclareOk("q1", 3, 0), "0032000B" + Q1 + "00000003" + "00000000"),
        (new Amqp0BasicPublish("", "q1") { Mandatory = true }, "003C0028" + "0000" + "00" + Q1 + "01"),
        (new Amqp0BasicGet("q1", NoAck: true), "003C0046" + "0000" + Q1 + "01"),
        (new Amqp0BasicGetOk(1, false, "", "q1", 0), "003C0047" + "0000000000000001" + "00" + "00" + Q1 + "00000000"),
        (new Amqp0BasicGetEmpty(), "003C0048" + "00"),
        (new Amqp0BasicAck(5, Multiple: false), "003C0050" + "0000000000000005" + "00"),
    ];

    [TestMethod]
    public void ToPayload_EveryMethod_WritesClassMethodAndArguments()
    {
        foreach ((Amqp0Method method, string hex) in MethodsAndBytes)
        {
            Assert.AreEqual(hex, Convert.ToHexString(method.ToPayload()), method.GetType().Name);
        }
    }

    [TestMethod]
    public void Read_EveryMethod_ReadsItBack()
    {
        foreach ((Amqp0Method method, string hex) in MethodsAndBytes)
        {
            Amqp0Method read = Amqp0MethodReader.Read(method.ToFrame(1));

            Assert.AreEqual(method.GetType(), read.GetType());
            Assert.AreEqual(hex, Convert.ToHexString(read.ToPayload()), method.GetType().Name);
        }
    }

    [TestMethod]
    public void Read_ValueMethods_EqualWhatWasWritten()
    {
        Amqp0Method[] methods =
        [
            new Amqp0ConnectionTune(2047, 131072, 60),
            new Amqp0ConnectionClose(320, "CONNECTION_FORCED", 10, 50),
            new Amqp0QueueDeclare("q") { Passive = true, Exclusive = true, NoWait = true },
            new Amqp0BasicPublish("amq.direct", "k") { Immediate = true },
            new Amqp0BasicGetOk(9, true, "x", "k", 4),
            new Amqp0BasicAck(7, Multiple: true),
        ];

        foreach (Amqp0Method method in methods)
        {
            Amqp0Method read = Amqp0MethodReader.Read(method.ToPayload());

            Assert.AreEqual(Convert.ToHexString(method.ToPayload()), Convert.ToHexString(read.ToPayload()));
        }

        Assert.AreEqual(new Amqp0BasicAck(7, true), Amqp0MethodReader.Read(methods[5].ToPayload()));
    }

    [TestMethod]
    public void ToFrame_IsAMethodFrameOnTheChannel()
    {
        Amqp0Frame frame = new Amqp0ConnectionCloseOk().ToFrame(3);

        Assert.AreEqual("010003000000040" + "00A0033CE", Convert.ToHexString(frame.ToBytes()));
    }

    [TestMethod]
    [DataRow("000A0034")]
    [DataRow("00630001")]
    public void Read_UnknownClassOrMethod_IsCommandInvalid(string hex)
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0MethodReader.Read(Convert.FromHexString(hex)));

        Assert.AreEqual(Amqp0ReplyCode.CommandInvalid, exception.ReplyCode);
    }

    [TestMethod]
    [DataRow("000A")]
    [DataRow("000A001E07FF")]
    [DataRow("000A003300")]
    public void Read_TruncatedOrOverlongArguments_IsSyntaxError(string hex)
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0MethodReader.Read(Convert.FromHexString(hex)));

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }

    [TestMethod]
    public void Read_NonMethodFrame_IsUnexpectedFrame()
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => Amqp0MethodReader.Read(new Amqp0Frame(Amqp0FrameType.Body, 1, [])));

        Assert.AreEqual(Amqp0ReplyCode.UnexpectedFrame, exception.ReplyCode);
    }
}
