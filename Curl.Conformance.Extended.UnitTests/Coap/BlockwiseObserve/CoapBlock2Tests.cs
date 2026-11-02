using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.BlockwiseObserve;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.BlockwiseObserve;

/// <summary>RFC 7959 sections 2.3, 2.4 and 3.1: curl fetches a Block2 response block by block, at the server's size, and joins it.</summary>
[TestClass]
public sealed class CoapBlock2Tests
{
    private static readonly byte[] Resource = CoapBlockwiseObserveRun.Body(2148);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7959-2.3-1")]
    [SpecRequirement("Coap:RFC7959-2.4-1")]
    [SpecRequirement("Coap:RFC7959-2.4-2")]
    [SpecRequirement("Coap:RFC7959-3.1-1")]
    public Task Block2Download_IsFetchedBlockByBlockAndWrittenWhole() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7959-2.3-1", "Coap:RFC7959-2.4-1", "Coap:RFC7959-2.4-2", "Coap:RFC7959-3.1-1"], async () =>
        {
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((request, _) => [BlockOf(request, 6, [1, 2, 3, 4])]);

            CurlRunResult result = await CoapBlockwiseObserveRun.RunAsync(server, TestContext.CancellationToken, CoapBlockwiseObserveRun.Url);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            CollectionAssert.AreEqual(Resource, result.StandardOutput);
            Assert.IsEmpty(result.StandardError);
            IReadOnlyList<CoapReceivedDatagram> sent = server.Received;
            Assert.HasCount(3, sent);
            Assert.IsNull(CoapBlockwiseObserveOptions.BlockOf(sent[0].Message, CoapBlockwiseObserveOptions.Block2));
            Assert.AreEqual(new CoapReferenceBlock(1, false, 6), CoapBlockwiseObserveOptions.BlockOf(sent[1].Message, CoapBlockwiseObserveOptions.Block2));
            Assert.AreEqual(new CoapReferenceBlock(2, false, 6), CoapBlockwiseObserveOptions.BlockOf(sent[2].Message, CoapBlockwiseObserveOptions.Block2));
            AssertFollowOnRequests(sent);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7959-2.4-3")]
    public Task ServerLowersSzxMidTransfer_LaterBlocksAreAskedAtTheSmallerSize() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7959-2.4-3"], async () =>
        {
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((request, index) => [BlockOf(request, index == 0 ? 6 : 5, [1])]);

            CurlRunResult result = await CoapBlockwiseObserveRun.RunAsync(server, TestContext.CancellationToken, CoapBlockwiseObserveRun.Url);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            CollectionAssert.AreEqual(Resource, result.StandardOutput);
            Assert.IsEmpty(result.StandardError);
            IReadOnlyList<CoapReceivedDatagram> sent = server.Received;
            Assert.HasCount(4, sent);
            Assert.AreEqual(new CoapReferenceBlock(1, false, 6), CoapBlockwiseObserveOptions.BlockOf(sent[1].Message, CoapBlockwiseObserveOptions.Block2));
            Assert.AreEqual(new CoapReferenceBlock(3, false, 5), CoapBlockwiseObserveOptions.BlockOf(sent[2].Message, CoapBlockwiseObserveOptions.Block2));
            Assert.AreEqual(new CoapReferenceBlock(4, false, 5), CoapBlockwiseObserveOptions.BlockOf(sent[3].Message, CoapBlockwiseObserveOptions.Block2));
            AssertFollowOnRequests(sent);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7959-2.4-4")]
    public Task EtagChangesBetweenBlocks_TheMixedBodyIsNotWrittenAndExit56() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7959-2.4-4"], async () =>
        {
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((request, index) => [BlockOf(request, 6, index == 0 ? [1] : [2])]);

            CurlRunResult result = await CoapBlockwiseObserveRun.RunAsync(server, TestContext.CancellationToken, CoapBlockwiseObserveRun.Url);

            Assert.AreEqual(56, result.ExitCode);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (56) ");
            Assert.IsEmpty(result.StandardOutput);
        });

    /// <summary>Answers a GET with the Block2 block it asks for (block 0 when it asks for none) at size exponent <paramref name="sizeExponent" />, as a piggybacked 2.05 with ETag <paramref name="etag" />.</summary>
    private static byte[] BlockOf(CoapReferenceMessage request, int sizeExponent, byte[] etag)
    {
        CoapReferenceBlock asked = CoapBlockwiseObserveOptions.BlockOf(request, CoapBlockwiseObserveOptions.Block2) ?? new CoapReferenceBlock(0, false, 6);
        int size = 1 << (sizeExponent + 4);
        int offset = asked.Offset;
        int length = Math.Min(size, Resource.Length - offset);
        bool more = offset + length < Resource.Length;
        return CoapResponses.Piggybacked(
            request,
            CoapResponses.Content,
            Resource[offset..(offset + length)],
            new CoapReferenceOption(CoapBlockwiseObserveOptions.ETag, etag),
            CoapBlockwiseObserveOptions.Block(CoapBlockwiseObserveOptions.Block2, new CoapReferenceBlock((uint)(offset / size), more, sizeExponent)));
    }

    /// <summary>Each later request is a new confirmable GET with the next Message ID, the first request's token and no payload.</summary>
    private static void AssertFollowOnRequests(IReadOnlyList<CoapReceivedDatagram> sent)
    {
        for (int index = 1; index < sent.Count; index++)
        {
            CoapReferenceMessage request = sent[index].Message;
            Assert.AreEqual(CoapReferenceMessageType.Confirmable, request.Type);
            Assert.AreEqual(sent[0].Message.Code, request.Code);
            Assert.AreEqual((ushort)(sent[0].Message.MessageId + index), request.MessageId);
            CollectionAssert.AreEqual(sent[0].Message.Token, request.Token);
            Assert.IsEmpty(request.Payload);
        }
    }
}
