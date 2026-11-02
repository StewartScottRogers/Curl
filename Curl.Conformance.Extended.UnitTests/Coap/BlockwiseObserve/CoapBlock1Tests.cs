using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.BlockwiseObserve;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.BlockwiseObserve;

/// <summary>RFC 7959 sections 2.3, 2.5 and 2.9: curl sends a large <c>-d</c> payload in Block1 blocks, following 2.31, 4.13 and 4.08.</summary>
[TestClass]
public sealed class CoapBlock1Tests
{
    private static readonly byte[] Payload = CoapBlockwiseObserveRun.Body(2500);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7959-2.2-1")]
    [SpecRequirement("Coap:RFC7959-2.5-1")]
    [SpecRequirement("Coap:RFC7959-2.5-2")]
    [SpecRequirement("Coap:RFC7959-2.9.1-1")]
    public Task Block1Upload_EachBlockFollowsA231Continue() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7959-2.2-1", "Coap:RFC7959-2.5-1", "Coap:RFC7959-2.5-2", "Coap:RFC7959-2.9.1-1"], async () =>
        {
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((request, _) => [Acknowledged(request, null)]);

            CurlRunResult result = await RunAsync(server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("stored", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            AssertBlocks(server.Received, 6, 3);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7959-2.5-3")]
    public Task ServerAsksForSmallerBlocksIn231_RemainingBlocksUseTheSmallerSize() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7959-2.5-3"], async () =>
        {
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((request, _) => [Acknowledged(request, 5)]);

            CurlRunResult result = await RunAsync(server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            IReadOnlyList<CoapReceivedDatagram> sent = server.Received;
            Assert.HasCount(4, sent);
            Assert.AreEqual(new CoapReferenceBlock(0, true, 6), Block1Of(sent[0]));
            Assert.AreEqual(new CoapReferenceBlock(2, true, 5), Block1Of(sent[1]));
            Assert.AreEqual(new CoapReferenceBlock(3, true, 5), Block1Of(sent[2]));
            Assert.AreEqual(new CoapReferenceBlock(4, false, 5), Block1Of(sent[3]));
            CollectionAssert.AreEqual(Payload, sent.SelectMany(datagram => datagram.Message.Payload).ToArray());
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7959-2.9.3-1")]
    [SpecRequirement("Coap:RFC7959-4-1")]
    public Task RequestEntityTooLargeWithSize1AndBlock1_PayloadIsSentAgainFromBlock0AtTheAskedSize() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7959-2.9.3-1", "Coap:RFC7959-4-1"], async () =>
        {
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((request, index) => index == 0
                ? [CoapResponses.Piggybacked(
                    request,
                    CoapBlockwiseObserveRun.RequestEntityTooLarge,
                    [],
                    CoapBlockwiseObserveOptions.Block(CoapBlockwiseObserveOptions.Block1, new CoapReferenceBlock(0, false, 5)),
                    CoapBlockwiseObserveOptions.Uint(CoapBlockwiseObserveOptions.Size1, 4096))]
                : [Acknowledged(request, null)]);

            CurlRunResult result = await RunAsync(server);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("stored", Encoding.UTF8.GetString(result.StandardOutput));
            IReadOnlyList<CoapReceivedDatagram> sent = server.Received;
            Assert.AreEqual(new CoapReferenceBlock(0, true, 6), Block1Of(sent[0]));
            AssertBlocks([.. sent.Skip(1)], 5, 5);
            Assert.AreEqual((ushort)(sent[0].Message.MessageId + 1), sent[1].Message.MessageId);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7959-2.9.2-1")]
    public Task RequestEntityIncompleteOnAMissingBlock_EndsTheUploadAndFailsUnderF() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7959-2.9.2-1"], async () =>
        {
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((request, index) => index == 0
                ? [Acknowledged(request, null)]
                : [CoapResponses.Piggybacked(request, CoapBlockwiseObserveRun.RequestEntityIncomplete, "missing"u8.ToArray())]);

            CurlRunResult result = await RunAsync(server, "-f");

            Assert.AreEqual(22, result.ExitCode);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (22) ");
            Assert.IsEmpty(result.StandardOutput);
            Assert.HasCount(2, server.Received);
        });

    private Task<CurlRunResult> RunAsync(CoapReferenceServer server, params string[] arguments) =>
        CoapBlockwiseObserveRun.RunAsync(server, TestContext.CancellationToken, [.. arguments, "-d", Encoding.ASCII.GetString(Payload), CoapBlockwiseObserveRun.Url]);

    /// <summary>Answers a Block1 block with 2.31 Continue echoing it (at <paramref name="askedSizeExponent" /> when set), or the last with 2.04 Changed and "stored".</summary>
    private static byte[] Acknowledged(CoapReferenceMessage request, int? askedSizeExponent)
    {
        CoapReferenceBlock block = CoapBlockwiseObserveOptions.BlockOf(request, CoapBlockwiseObserveOptions.Block1)!;
        CoapReferenceOption echo = CoapBlockwiseObserveOptions.Block(CoapBlockwiseObserveOptions.Block1, block with { SizeExponent = askedSizeExponent ?? block.SizeExponent });
        return block.More
            ? CoapResponses.Piggybacked(request, CoapBlockwiseObserveRun.Continue, [], echo)
            : CoapResponses.Piggybacked(request, CoapBlockwiseObserveRun.Changed, "stored"u8.ToArray(), echo);
    }

    private static CoapReferenceBlock? Block1Of(CoapReceivedDatagram datagram) =>
        CoapBlockwiseObserveOptions.BlockOf(datagram.Message, CoapBlockwiseObserveOptions.Block1);

    /// <summary>The datagrams are the payload's blocks of size exponent <paramref name="sizeExponent" /> in order, M set on all but the last, each a POST with the next Message ID and one token.</summary>
    private static void AssertBlocks(IReadOnlyList<CoapReceivedDatagram> sent, int sizeExponent, int count)
    {
        Assert.HasCount(count, sent);
        int size = 1 << (sizeExponent + 4);
        for (int index = 0; index < count; index++)
        {
            CoapReferenceMessage request = sent[index].Message;
            Assert.AreEqual(new CoapReferenceBlock((uint)index, index < count - 1, sizeExponent), Block1Of(sent[index]));
            CollectionAssert.AreEqual(Payload[(index * size)..Math.Min((index + 1) * size, Payload.Length)], request.Payload);
            Assert.AreEqual(CoapReferenceMessageType.Confirmable, request.Type);
            Assert.AreEqual((byte)0x02, request.Code);
            Assert.AreEqual((ushort)(sent[0].Message.MessageId + index), request.MessageId);
            CollectionAssert.AreEqual(sent[0].Message.Token, request.Token);
        }
    }
}
