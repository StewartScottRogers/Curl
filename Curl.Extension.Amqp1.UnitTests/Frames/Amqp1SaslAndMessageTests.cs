using Curl.Extension.Amqp1.Types;

namespace Curl.Extension.Amqp1.Frames;

[TestClass]
public sealed class Amqp1SaslAndMessageTests
{
    [TestMethod]
    public void SaslMechanisms_PinsBytesAndReadsBack_Part5Section5_3_3_1()
    {
        var mechanisms = new Amqp1Array(Amqp1FormatCode.Sym8, [Amqp1SaslInitialResponse.Plain, Amqp1SaslInitialResponse.Anonymous]);
        var body = Amqp1Composite.Create(Amqp1CompositeType.SaslMechanisms, ("sasl-server-mechanisms", mechanisms));
        const string hex = "0000002202010000" + "005340C01501" + "E01202A3" + "05504C41494E" + "09414E4F4E594D4F5553";

        Assert.AreEqual(hex, Convert.ToHexString(Amqp1FrameCodec.Write(Amqp1Frame.ForSasl(body))));

        Assert.IsTrue(Amqp1FrameCodec.TryRead(Convert.FromHexString(hex), out var frame, out _));
        var (read, _) = frame!.ReadBody();
        var offered = (Amqp1Array)read["sasl-server-mechanisms"]!;
        CollectionAssert.AreEqual(new object[] { new Amqp1Symbol("PLAIN"), new Amqp1Symbol("ANONYMOUS") }, offered.Elements.ToArray());
    }

    [TestMethod]
    public void SaslInit_WithPlainInitialResponse_PinsBytes_Part5Section5_3_3_2_Rfc4616()
    {
        var init = Amqp1Composite.Create(Amqp1CompositeType.SaslInit,
            ("mechanism", Amqp1SaslInitialResponse.Plain), ("initial-response", Amqp1SaslInitialResponse.ForPlain("", "u", "p")));

        Assert.AreEqual(
            "0000001B02010000" + "005341C00E02" + "A305504C41494E" + "A00400750070",
            Convert.ToHexString(Amqp1FrameCodec.Write(Amqp1Frame.ForSasl(init))));
    }

    [TestMethod]
    public void SaslInitialResponses_PlainJoinsWithNul_AnonymousIsTheTrace()
    {
        Assert.AreEqual("6100750070", Convert.ToHexString(Amqp1SaslInitialResponse.ForPlain("a", "u", "p")));
        Assert.AreEqual("74", Convert.ToHexString(Amqp1SaslInitialResponse.ForAnonymous("t")));
        Assert.IsEmpty(Amqp1SaslInitialResponse.ForAnonymous(""));
    }

    [TestMethod]
    [DataRow("\0", "u", "p", DisplayName = "NUL in the authorization identity")]
    [DataRow("", "u\0", "p", DisplayName = "NUL in the user name")]
    [DataRow("", "u", "\0", DisplayName = "NUL in the password")]
    public void ForPlain_PartHoldsNul_Throws(string authorizationIdentity, string userName, string password)
    {
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(
            () => Amqp1SaslInitialResponse.ForPlain(authorizationIdentity, userName, password));
    }

    [TestMethod]
    [DataRow("challenge", "005342C00501" + "A0020102", DisplayName = "sasl-challenge 5.3.3.3")]
    [DataRow("response", "005343C00501" + "A0020102", DisplayName = "sasl-response 5.3.3.4")]
    [DataRow("outcome", "005344C00301" + "5000", DisplayName = "sasl-outcome 5.3.3.5")]
    public void SaslFrameBody_PinsBytesAndReadsBack(string name, string hex)
    {
        var body = name switch
        {
            "challenge" => Amqp1Composite.Create(Amqp1CompositeType.SaslChallenge, ("challenge", new byte[] { 1, 2 })),
            "response" => Amqp1Composite.Create(Amqp1CompositeType.SaslResponse, ("response", new byte[] { 1, 2 })),
            _ => Amqp1Composite.Create(Amqp1CompositeType.SaslOutcome, ("code", (byte)0)),
        };
        var frame = Amqp1Frame.ForSasl(body);

        var (read, payload) = frame.ReadBody();

        Assert.AreEqual(hex, Convert.ToHexString(frame.Body.Span));
        Assert.AreSame(body.Type, read.Type);
        Assert.AreEqual(Amqp1CompositeKind.Sasl, read.Type.Kind);
        Assert.IsTrue(payload.IsEmpty);
    }

    [TestMethod]
    public void Sections_PinBytesPerSection_Part3Section3_2()
    {
        var sections = Sections();
        const string hex = "005370C00201" + "41"
            + "005373C00803" + "A1016D" + "40" + "A10171"
            + "005374C10602" + "A1016B" + "5401"
            + "005375A0026869"
            + "005376C00301" + "5201"
            + "005377A1026869";

        Assert.AreEqual(hex, Convert.ToHexString(Amqp1MessageCodec.Write(sections)));
    }

    [TestMethod]
    public void Read_MessageSplitOverTwoTransfers_ReadsEverySectionBack_Part2Section2_6_14()
    {
        var payload = Amqp1MessageCodec.Write(Sections());
        var first = Amqp1Composite.Create(Amqp1CompositeType.Transfer, ("handle", 0u), ("delivery-id", 0u), ("more", true));
        var second = Amqp1Composite.Create(Amqp1CompositeType.Transfer, ("handle", 0u));
        var wire = Amqp1FrameCodec.Write(Amqp1Frame.ForPerformative(1, first, payload.AsSpan(0, 10)))
            .Concat(Amqp1FrameCodec.Write(Amqp1Frame.ForPerformative(1, second, payload.AsSpan(10)))).ToArray();
        var payloads = new List<ReadOnlyMemory<byte>>();
        ReadOnlyMemory<byte> buffer = wire;
        while (Amqp1FrameCodec.TryRead(buffer, out var frame, out var consumed))
        {
            payloads.Add(frame!.ReadBody().Payload);
            buffer = buffer[consumed..];
        }

        var sections = Amqp1MessageCodec.Read(payloads);

        Assert.HasCount(2, payloads);
        CollectionAssert.AreEqual(Sections().Select(section => section.Type).ToArray(), sections.Select(section => section.Type).ToArray());
        Assert.AreEqual(true, ((Amqp1Composite)sections[0].Value!)["durable"]);
        Assert.AreEqual("q", ((Amqp1Composite)sections[1].Value!)["to"]);
        Assert.AreEqual(1, ((Amqp1Map)sections[2].Value!).Entries[0].Value);
        CollectionAssert.AreEqual(new byte[] { 0x68, 0x69 }, (byte[])sections[3].Value!);
        Assert.AreEqual(1u, ((IReadOnlyList<object?>)sections[4].Value!)[0]);
        Assert.AreEqual("hi", sections[5].Value);
    }

    [TestMethod]
    public void Read_AnnotationsFooterAndSymbolicDescriptors_ReadBack()
    {
        var map = new Amqp1Map([new(new Amqp1Symbol("x-k"), "v")]);
        var bytes = Amqp1MessageCodec.Write(
        [
            new(Amqp1SectionType.DeliveryAnnotations, map), new(Amqp1SectionType.MessageAnnotations, map), new(Amqp1SectionType.Footer, map),
        ]).Concat(Amqp1Writer.Encode(new Amqp1Described(new Amqp1Symbol("amqp:data:binary"), new byte[] { 1 }))).ToArray();

        var sections = Amqp1MessageCodec.Read([bytes]);

        CollectionAssert.AreEqual(
            new[] { Amqp1SectionType.DeliveryAnnotations, Amqp1SectionType.MessageAnnotations, Amqp1SectionType.Footer, Amqp1SectionType.Data },
            sections.Select(section => section.Type).ToArray());
        Assert.AreEqual(new Amqp1Symbol("amqp:amqp-value:*"), Amqp1SectionType.AmqpValue.Symbol);
        Assert.AreEqual("footer", Amqp1SectionType.Footer.ToString());
        Assert.AreSame(Amqp1CompositeType.Header, Amqp1SectionType.Header.CompositeType);
        Assert.IsNull(Amqp1SectionType.Data.CompositeType);
    }

    [TestMethod]
    public void Write_SectionHoldingTheWrongValue_Throws()
    {
        var end = Amqp1Composite.Create(Amqp1CompositeType.End);

        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1MessageCodec.Write([new(Amqp1SectionType.Data, "text")]));
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1MessageCodec.Write([new(Amqp1SectionType.Header, end)]));
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1MessageCodec.Write([new(Amqp1SectionType.Header, null)]));
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1MessageCodec.Write([new(Amqp1SectionType.Footer, 1)]));
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1MessageCodec.Write([new(Amqp1SectionType.AmqpSequence, 1)]));
    }

    [TestMethod]
    [DataRow("40", DisplayName = "not described")]
    [DataRow("A10178", DisplayName = "a string, not described")]
    [DataRow("004045", DisplayName = "null descriptor")]
    [DataRow("00531045", DisplayName = "a performative, not a section")]
    [DataRow("005375A10168", DisplayName = "data holding a string")]
    [DataRow("005370A10168", DisplayName = "header holding a string")]
    public void Read_NotAWellFormedSection_Throws(string hex)
    {
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1MessageCodec.Read([Convert.FromHexString(hex)]));
    }

    private static Amqp1Section[] Sections() =>
    [
        new(Amqp1SectionType.Header, Amqp1Composite.Create(Amqp1CompositeType.Header, ("durable", true))),
        new(Amqp1SectionType.Properties, Amqp1Composite.Create(Amqp1CompositeType.Properties, ("message-id", "m"), ("to", "q"))),
        new(Amqp1SectionType.ApplicationProperties, new Amqp1Map([new("k", 1)])),
        new(Amqp1SectionType.Data, new byte[] { 0x68, 0x69 }),
        new(Amqp1SectionType.AmqpSequence, new object?[] { 1u }),
        new(Amqp1SectionType.AmqpValue, "hi"),
    ];
}
