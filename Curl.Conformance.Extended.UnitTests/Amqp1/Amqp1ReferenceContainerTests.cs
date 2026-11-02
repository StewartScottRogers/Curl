using System.Buffers.Binary;
using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Amqp1;

/// <summary>The reference container's own edges, its encoder and decoder, that no curl run reaches.</summary>
[TestClass]
public sealed class Amqp1ReferenceContainerTests
{
    private static readonly Amqp1ReferenceEncoder Narrow = new(false);

    private static readonly Amqp1ReferenceEncoder Wide = new(true);

    public static IEnumerable<object?[]> RoundTripValues =>
    [
        [null], [true], [false], [(byte)7], [(ushort)513], [0u], [9u], [70000u], [0ul], [9ul], [70000ul],
        ["text"], [new string('x', 300)], [new Amqp1ReferenceSymbol("PLAIN")], [new byte[] { 1, 2 }],
        [new Amqp1ReferenceDescribed(Amqp1ReferenceDescriptors.Open, new object?[] { "id", null, 5u })],
        [Array.Empty<object?>()], [new object?[] { new string('y', 300) }],
    ];

    [TestMethod]
    [DynamicData(nameof(RoundTripValues))]
    public void Decode_EncodedNarrowOrWide_ReadsTheSameValue(object? value)
    {
        foreach (Amqp1ReferenceEncoder encoder in new[] { Narrow, Wide })
        {
            byte[] bytes = encoder.Encode(value);
            int position = 0;

            object? decoded = Amqp1ReferenceDecoder.Read(bytes, ref position);

            Assert.AreEqual(bytes.Length, position);
            Assert.AreEqual(Normalize(value), Normalize(decoded));
        }
    }

    [TestMethod]
    [DataRow(new byte[] { 0x56, 0x01 }, true)]
    [DataRow(new byte[] { 0x56, 0x00 }, false)]
    public void Decode_BooleanWithAByte_ReadsIt(byte[] bytes, bool expected)
    {
        int position = 0;

        Assert.AreEqual(expected, Amqp1ReferenceDecoder.Read(bytes, ref position));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0xa1, 0x01, 0x61, 0x40 })]
    [DataRow(new byte[] { 0x98 })]
    [DataRow(new byte[] { 0x46 })]
    [DataRow(new byte[] { 0x51, 0x01 })]
    [DataRow(new byte[] { 0xa2, 0x00 })]
    [DataRow(new byte[] { 0xa5, 0x00 })]
    [DataRow(new byte[] { 0xc1, 0x01, 0x00 })]
    [DataRow(new byte[] { 0xe0, 0x03, 0x01, 0x50, 0x01 })]
    public void Decode_FormatCodeNotRead_Throws(byte[] bytes)
    {
        int position = 0;

        Assert.ThrowsExactly<InvalidDataException>(() => Amqp1ReferenceDecoder.Read(bytes, ref position));
    }

    [TestMethod]
    public void Encode_SymbolArray_WritesArrayNarrowOrWide()
    {
        Amqp1ReferenceSymbol[] symbols = [new("AB")];

        CollectionAssert.AreEqual(new byte[] { 0xe0, 0x05, 0x01, 0xa3, 0x02, 0x41, 0x42 }, Narrow.Encode(symbols));
        CollectionAssert.AreEqual(new byte[] { 0xf0, 0, 0, 0, 0x0b, 0, 0, 0, 1, 0xb3, 0, 0, 0, 2, 0x41, 0x42 }, Wide.Encode(symbols));
        Assert.AreEqual(0xf0, Narrow.Encode(new Amqp1ReferenceSymbol[] { new(new string('s', 300)) })[0]);
    }

    [TestMethod]
    public void Encode_UnknownType_Throws() => Assert.ThrowsExactly<ArgumentException>(() => Narrow.Encode(1.5));

    [TestMethod]
    public void Field_ValueNotAList_IsNull() => Assert.IsNull(new Amqp1ReferenceDescribed(1, "x").Field(0));

    [TestMethod]
    public async Task ServeAsync_FirstHeaderNotSasl_AnswersSaslHeaderAndCloses()
    {
        (Amqp1ReferenceContainer container, byte[] sent) = await ServeAsync(new(), Amqp1ReferenceSession.Header(0));

        CollectionAssert.AreEqual(Amqp1ReferenceSession.Header(3), sent);
        Assert.HasCount(1, container.Headers);
    }

    [TestMethod]
    public async Task ServeAsync_ClosedBeforeAnyHeader_SendsNothing()
    {
        (_, byte[] plain) = await ServeAsync(new());
        (_, byte[] answering) = await ServeAsync(new() { AnswerHeaderProtocolId = 0 });

        Assert.IsEmpty(plain);
        Assert.IsEmpty(answering);
    }

    [TestMethod]
    public async Task ServeAsync_FrameOtherThanSaslInit_EndsAfterMechanisms()
    {
        (Amqp1ReferenceContainer container, byte[] sent) = await ServeAsync(new(), [.. Amqp1ReferenceSession.Header(3), .. Frame(1, 0, Body(Amqp1ReferenceDescriptors.Open))]);

        Assert.HasCount(1, container.Frames);
        Assert.HasCount(1, (await ReadAll(sent)).Frames);
    }

    [TestMethod]
    [DataRow(null, null, (byte)1)]
    [DataRow("EXTERNAL", null, (byte)1)]
    [DataRow("PLAIN", "alice", (byte)1)]
    [DataRow("PLAIN", null, (byte)1)]
    [DataRow("PLAIN", "\0bob\0secret", (byte)1)]
    [DataRow("PLAIN", "\0alice\0wrong", (byte)1)]
    [DataRow("ANONYMOUS", null, (byte)0)]
    [DataRow("PLAIN", "x\0alice\0secret", (byte)0)]
    public async Task ServeAsync_SaslInit_AnswersOutcomeByMechanismAndCredentials(string? mechanism, string? response, byte expected)
    {
        object? symbol = mechanism is null ? null : new Amqp1ReferenceSymbol(mechanism);
        object? initial = response is null ? null : System.Text.Encoding.UTF8.GetBytes(response);

        (_, byte[] sent) = await ServeAsync(new(), [.. Amqp1ReferenceSession.Header(3), .. Frame(1, 0, Body(Amqp1ReferenceDescriptors.SaslInit, symbol, initial))]);

        Assert.AreEqual(expected, (await ReadAll(sent)).Frames[1].Body!.Field(0));
    }

    [TestMethod]
    public async Task ServeAsync_ForcedOutcome_AnswersItWhateverTheInit()
    {
        (_, byte[] sent) = await ServeAsync(new() { SaslOutcomeCode = 2 }, SaslOk());

        Assert.AreEqual((byte)2, (await ReadAll(sent)).Frames[1].Body!.Field(0));
    }

    [TestMethod]
    public async Task ServeAsync_SecondHeaderMissingOrWrong_StopsAfterIt()
    {
        (Amqp1ReferenceContainer missing, _) = await ServeAsync(new(), SaslOk());
        (Amqp1ReferenceContainer wrong, byte[] sent) = await ServeAsync(new(), [.. SaslOk(), .. Amqp1ReferenceSession.Header(3)]);

        Assert.HasCount(1, missing.Headers);
        Assert.HasCount(2, wrong.Headers);
        Assert.AreEqual(2, (await ReadAll(sent)).Headers);
    }

    [TestMethod]
    public async Task ServeAsync_Connection_AnswersEachPerformativeUntilClose()
    {
        byte[] extendedEmpty = [0, 0, 0, 12, 3, 0, 0, 0, 0, 0, 0, 0];
        byte[] client =
        [
            .. SaslOk(), .. Amqp1ReferenceSession.Header(0), .. Frame(0, 0, []), .. extendedEmpty,
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Open, "client")),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Begin, null, 0u, 10u, 10u)),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Attach, "l", 0u, false)),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Flow), [9]),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Transfer)),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Error)),
            .. Frame(0, 0, Body(1)),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Detach, 0u, true)),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.End)),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Close)),
            .. Frame(0, 0, Body(Amqp1ReferenceDescriptors.Open)),
        ];

        (Amqp1ReferenceContainer container, byte[] sent) = await ServeAsync(new() { MaxFrameSize = 512, IdleTimeOut = 1000, WideEncoding = true }, client);

        ulong[] answered = [.. (await ReadAll(sent)).Frames.Skip(2).Select(frame => frame.Body!.Descriptor)];
        CollectionAssert.AreEqual(
            new[] { Amqp1ReferenceDescriptors.Open, Amqp1ReferenceDescriptors.Begin, Amqp1ReferenceDescriptors.Attach, Amqp1ReferenceDescriptors.Detach, Amqp1ReferenceDescriptors.Detach, Amqp1ReferenceDescriptors.End, Amqp1ReferenceDescriptors.Close },
            answered);
        Assert.IsNull((await ReadAll(sent)).Frames[4].Body!.Field(9));
        Assert.AreEqual(512u, (await ReadAll(sent)).Frames[2].Body!.Field(2));
        CollectionAssert.AreEqual(new byte[] { 9 }, container.Frames.Single(frame => frame.Body?.Descriptor == Amqp1ReferenceDescriptors.Flow).Payload);
    }

    [TestMethod]
    public async Task ServeAsync_ClosedInsideAFrame_Ends()
    {
        (Amqp1ReferenceContainer container, _) = await ServeAsync(new(), [.. Amqp1ReferenceSession.Header(3), 0, 0, 0, 20, 2, 1]);

        Assert.IsEmpty(container.Frames);
    }

    [TestMethod]
    public async Task ServeAsync_FrameBodyNotDescribed_Throws() =>
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ServeAsync(new(), [.. Amqp1ReferenceSession.Header(3), .. Frame(1, 0, Narrow.Encode("x"))]));

    [TestMethod]
    public async Task AttachedAsync_AttachWithoutBody_DetachesWithoutHandle()
    {
        using DuplexStream stream = new([]);
        Amqp1ReferenceSession session = new(new ReferenceConnection(stream, new WireExchange(5672, ReferenceTransport.Tcp)), Narrow, CancellationToken.None);

        await new Amqp1ReferenceLinkRefusal().AttachedAsync(session, new Amqp1ReferenceFrame(0, 0, null, []));

        Assert.IsNull((await ReadAll([.. Amqp1ReferenceSession.Header(0), .. stream.Written])).Frames[0].Body!.Field(0));
    }

    private static byte[] SaslOk() =>
        [.. Amqp1ReferenceSession.Header(3), .. Frame(1, 0, Body(Amqp1ReferenceDescriptors.SaslInit, new Amqp1ReferenceSymbol("ANONYMOUS")))];

    private static byte[] Body(ulong descriptor, params object?[] fields) => Narrow.Encode(new Amqp1ReferenceDescribed(descriptor, fields));

    private static byte[] Frame(byte type, ushort channel, byte[] body, byte[]? payload = null)
    {
        byte[] frame = new byte[8 + body.Length + (payload?.Length ?? 0)];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)frame.Length);
        frame[4] = 2;
        frame[5] = type;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(6), channel);
        body.CopyTo(frame, 8);
        payload?.CopyTo(frame, 8 + body.Length);
        return frame;
    }

    private static async Task<(Amqp1ReferenceContainer Container, byte[] Sent)> ServeAsync(Amqp1ReferenceContainerOptions options, params byte[] client)
    {
        Amqp1ReferenceContainer container = new(options);
        using DuplexStream stream = new(client);

        await container.ServeAsync(new ReferenceConnection(stream, new WireExchange(5672, ReferenceTransport.Tcp)), CancellationToken.None);

        return (container, stream.Written);
    }

    // The headers and frames the container sent, read back with its own session.
    private static async Task<(int Headers, List<Amqp1ReferenceFrame> Frames)> ReadAll(byte[] sent)
    {
        using DuplexStream stream = new(sent);
        Amqp1ReferenceSession reader = new(new ReferenceConnection(stream, new WireExchange(5672, ReferenceTransport.Tcp)), Narrow, CancellationToken.None);
        int headers = 0;
        List<Amqp1ReferenceFrame> frames = [];
        while (stream.ReadPosition < sent.Length)
        {
            if (sent.AsSpan(stream.ReadPosition).StartsWith("AMQP"u8))
            {
                await reader.ReadHeaderAsync();
                headers++;
            }
            else
            {
                frames.Add((await reader.ReadFrameAsync())!);
            }
        }

        return (headers, frames);
    }

    private static string Normalize(object? value) => value switch
    {
        null => "null",
        byte[] bytes => Convert.ToHexString(bytes),
        object?[] list => "[" + string.Join(",", list.Select(Normalize)) + "]",
        Amqp1ReferenceDescribed described => $"{described.Descriptor}:{Normalize(described.Value)}",
        _ => $"{value.GetType().Name}:{value}",
    };

    // Reads from the client's bytes and keeps what the container writes, apart, as a socket does.
    private sealed class DuplexStream(byte[] input) : Stream
    {
        private readonly MemoryStream written = new();

        public byte[] Written => written.ToArray();

        public int ReadPosition { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = Math.Min(count, input.Length - ReadPosition);
            Array.Copy(input, ReadPosition, buffer, offset, read);
            ReadPosition += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => written.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            written.Dispose();
            base.Dispose(disposing);
        }
    }
}
