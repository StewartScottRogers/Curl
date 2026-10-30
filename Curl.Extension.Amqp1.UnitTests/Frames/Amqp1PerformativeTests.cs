using Curl.Extension.Amqp1.Types;

namespace Curl.Extension.Amqp1.Frames;

[TestClass]
public sealed class Amqp1PerformativeTests
{
    private static string Encode(Amqp1Composite composite) => Convert.ToHexString(Amqp1Writer.Encode(composite.ToDescribed()));

    private static Amqp1Composite Decode(string hex) => Amqp1Composite.Read(Amqp1Reader.Decode(Convert.FromHexString(hex)));

    [TestMethod]
    public void Open_InAFrame_PinsBytesAndReadsBack_Section2_7_1()
    {
        var open = Amqp1Composite.Create(Amqp1CompositeType.Open,
            ("container-id", "c"), ("hostname", "h"), ("max-frame-size", 65536u), ("channel-max", (ushort)255));
        const string hex = "0000001C02000000" + "005310C00F04" + "A10163" + "A10168" + "7000010000" + "6000FF";

        Assert.AreEqual(hex, Convert.ToHexString(Amqp1FrameCodec.Write(Amqp1Frame.ForPerformative(0, open))));

        Assert.IsTrue(Amqp1FrameCodec.TryRead(Convert.FromHexString(hex), out var frame, out _));
        var (performative, payload) = frame!.ReadBody();
        Assert.AreSame(Amqp1CompositeType.Open, performative.Type);
        Assert.AreEqual("c", performative["container-id"]);
        Assert.AreEqual(65536u, performative["max-frame-size"]);
        Assert.AreEqual((ushort)255, performative["channel-max"]);
        Assert.IsNull(performative["idle-time-out"]);
        Assert.IsTrue(payload.IsEmpty);
    }

    [TestMethod]
    [DataRow("begin", "005311C00E04" + "40" + "5201" + "7000000800" + "7000000800", DisplayName = "begin 2.7.2")]
    [DataRow("attach", "005312C00603" + "A1016C" + "43" + "41", DisplayName = "attach 2.7.3")]
    [DataRow("flow", "005313C00B07" + "40" + "5201" + "43" + "5201" + "43" + "43" + "520A", DisplayName = "flow 2.7.4")]
    [DataRow("transfer", "005314C00805" + "43" + "43" + "A00101" + "43" + "42", DisplayName = "transfer 2.7.5")]
    [DataRow("disposition", "005315C00905" + "41" + "43" + "43" + "41" + "00532445", DisplayName = "disposition 2.7.6")]
    [DataRow("detach", "005316C00302" + "43" + "41", DisplayName = "detach 2.7.7")]
    [DataRow("end", "00531745", DisplayName = "end 2.7.8")]
    [DataRow("close", "005318C01F01" + "00531DC01902" + "A313616D71703A696E7465726E616C2D6572726F72" + "A10178", DisplayName = "close and error 2.7.9, 2.8.14")]
    public void Performative_PinsBytesAndReadsBack(string name, string hex)
    {
        var composite = Performatives()[name];

        Assert.AreEqual(hex, Encode(composite));

        var decoded = Decode(hex);
        Assert.AreSame(composite.Type, decoded.Type);
        Assert.AreEqual(hex, Encode(decoded));
    }

    [TestMethod]
    public void Close_ReadsItsErrorAsAComposite()
    {
        var close = Decode("005318C01F01" + "00531DC01902" + "A313616D71703A696E7465726E616C2D6572726F72" + "A10178");

        var error = Amqp1Composite.Read(close["error"]);

        Assert.AreSame(Amqp1CompositeType.Error, error.Type);
        Assert.AreEqual(new Amqp1Symbol("amqp:internal-error"), error["condition"]);
        Assert.AreEqual("x", error["description"]);
    }

    [TestMethod]
    [DataRow("received", "005323C00402" + "43" + "5301", DisplayName = "received 3.4.1")]
    [DataRow("accepted", "00532445", DisplayName = "accepted 3.4.2")]
    [DataRow("rejected", "005325C01001" + "00531DC00A01" + "A307616D71703A6E6F", DisplayName = "rejected 3.4.3")]
    [DataRow("released", "00532645", DisplayName = "released 3.4.4")]
    [DataRow("modified", "005327C00201" + "41", DisplayName = "modified 3.4.5")]
    public void DeliveryState_PinsBytesAndReadsBack_Part3Section3_4(string name, string hex)
    {
        var composite = DeliveryStates()[name];

        Assert.AreEqual(hex, Encode(composite));

        var decoded = Decode(hex);
        Assert.AreSame(composite.Type, decoded.Type);
        Assert.AreEqual(Amqp1CompositeKind.DeliveryState, decoded.Type.Kind);
    }

    [TestMethod]
    public void Read_SymbolicDescriptor_NamesTheSameType()
    {
        var bytes = Amqp1Writer.Encode(new Amqp1Described(new Amqp1Symbol("amqp:open:list"), new object?[] { "c" }));

        var open = Amqp1Composite.Read(Amqp1Reader.Decode(bytes));

        Assert.AreSame(Amqp1CompositeType.Open, open.Type);
        Assert.AreEqual(new Amqp1Symbol("amqp:open:list"), Amqp1CompositeType.Open.Symbol);
        Assert.AreEqual("open", Amqp1CompositeType.Open.ToString());
    }

    [TestMethod]
    [DataRow("40", DisplayName = "not described")]
    [DataRow("A10178", DisplayName = "a string, not described")]
    [DataRow("004045", DisplayName = "null descriptor")]
    [DataRow("00539945", DisplayName = "unknown descriptor")]
    [DataRow("00A3036E6F7445", DisplayName = "unknown symbolic descriptor")]
    [DataRow("00531040", DisplayName = "value not a list")]
    [DataRow("00531045", DisplayName = "open with no container-id")]
    [DataRow("005310C00201" + "40", DisplayName = "open with a null container-id")]
    [DataRow("005323C00201" + "43", DisplayName = "received with no section-offset")]
    public void Read_NotAWellFormedComposite_Throws(string hex)
    {
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Decode(hex));
    }

    [TestMethod]
    public void Create_MissingMandatoryField_Throws_UnknownFieldName_IsRefused()
    {
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Composite.Create(Amqp1CompositeType.Open, ("hostname", "h")));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Amqp1Composite.Create(Amqp1CompositeType.End, ("nothing", 1)));
    }

    [TestMethod]
    public void Transfer_CarriesItsPayload_OtherFramesCarryNone()
    {
        var transfer = Amqp1Composite.Create(Amqp1CompositeType.Transfer, ("handle", 0u));
        var end = Amqp1Composite.Create(Amqp1CompositeType.End);
        var frame = Amqp1Frame.ForPerformative(7, transfer, [0x00, 0x53, 0x77, 0x40]);

        var (performative, payload) = frame.ReadBody();

        Assert.AreEqual((ushort)7, frame.Channel);
        Assert.AreSame(Amqp1CompositeType.Transfer, performative.Type);
        Assert.AreEqual("00537740", Convert.ToHexString(payload.Span));
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Frame.ForPerformative(0, end, [0x40]));
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(
            () => new Amqp1Frame(Amqp1Frame.AmqpType, 0, Convert.FromHexString("0053174540")).ReadBody());
    }

    [TestMethod]
    public void Frames_CarryOnlyTheirOwnKind()
    {
        var open = Amqp1Composite.Create(Amqp1CompositeType.Open, ("container-id", "c"));
        var outcome = Amqp1Composite.Create(Amqp1CompositeType.SaslOutcome, ("code", (byte)0));

        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Frame.ForSasl(open));
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1Frame.ForPerformative(0, outcome));
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(
            () => new Amqp1Frame(Amqp1Frame.AmqpType, 0, Amqp1Writer.Encode(outcome.ToDescribed())).ReadBody());
    }

    [TestMethod]
    public void Catalogue_HoldsEveryTypeOnceUnderItsCode()
    {
        Assert.HasCount(24, Amqp1CompositeType.All);
        Assert.HasCount(24, Amqp1CompositeType.All.Select(type => type.Code).Distinct());
        foreach (var type in Amqp1CompositeType.All)
        {
            Assert.AreSame(type, Amqp1CompositeType.FromDescriptor(type.Code));
            Assert.AreSame(type, Amqp1CompositeType.FromDescriptor(type.Symbol));
        }
    }

    private static Dictionary<string, Amqp1Composite> Performatives() => new()
    {
        ["begin"] = Amqp1Composite.Create(Amqp1CompositeType.Begin, ("next-outgoing-id", 1u), ("incoming-window", 2048u), ("outgoing-window", 2048u)),
        ["attach"] = Amqp1Composite.Create(Amqp1CompositeType.Attach, ("name", "l"), ("handle", 0u), ("role", true)),
        ["flow"] = Amqp1Composite.Create(Amqp1CompositeType.Flow,
            ("incoming-window", 1u), ("next-outgoing-id", 0u), ("outgoing-window", 1u), ("handle", 0u), ("delivery-count", 0u), ("link-credit", 10u)),
        ["transfer"] = Amqp1Composite.Create(Amqp1CompositeType.Transfer,
            ("handle", 0u), ("delivery-id", 0u), ("delivery-tag", new byte[] { 1 }), ("message-format", 0u), ("settled", false)),
        ["disposition"] = Amqp1Composite.Create(Amqp1CompositeType.Disposition,
            ("role", true), ("first", 0u), ("last", 0u), ("settled", true), ("state", Amqp1Composite.Create(Amqp1CompositeType.Accepted).ToDescribed())),
        ["detach"] = Amqp1Composite.Create(Amqp1CompositeType.Detach, ("handle", 0u), ("closed", true)),
        ["end"] = Amqp1Composite.Create(Amqp1CompositeType.End),
        ["close"] = Amqp1Composite.Create(Amqp1CompositeType.Close, ("error", Amqp1Composite.Create(Amqp1CompositeType.Error,
            ("condition", new Amqp1Symbol("amqp:internal-error")), ("description", "x")).ToDescribed())),
    };

    private static Dictionary<string, Amqp1Composite> DeliveryStates() => new()
    {
        ["received"] = Amqp1Composite.Create(Amqp1CompositeType.Received, ("section-number", 0u), ("section-offset", 1ul)),
        ["accepted"] = Amqp1Composite.Create(Amqp1CompositeType.Accepted),
        ["rejected"] = Amqp1Composite.Create(Amqp1CompositeType.Rejected, ("error", Amqp1Composite.Create(Amqp1CompositeType.Error,
            ("condition", new Amqp1Symbol("amqp:no"))).ToDescribed())),
        ["released"] = Amqp1Composite.Create(Amqp1CompositeType.Released),
        ["modified"] = Amqp1Composite.Create(Amqp1CompositeType.Modified, ("delivery-failed", true)),
    };
}
