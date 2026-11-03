using System.Buffers.Binary;
using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1ProtocolHeaderCases;

namespace Curl.Conformance.Extended.UnitTests.Amqp1;

/// <summary>Part 2 sections 2.4 to 2.6: the order of open, begin, attach, detach, end and close curl sends for <c>-d</c>, and a link the container refuses.</summary>
[TestClass]
public sealed class Amqp1SessionLinkCases
{
    private const ulong Accepted = 0x24;

    internal static Task<CurlRunResult> RunAsync(Amqp1ReferenceContainer container, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(5672, container)], ["-sS", "-m", "10", "amqp1://localhost/queue", .. arguments]);

    /// <summary>The AMQP frames (type 0) among the bytes curl sent, each whole, with its eight-byte header.</summary>
    internal static List<byte[]> ClientAmqpFrames(byte[] clientBytes)
    {
        List<byte[]> result = [];
        for (int at = 0; at < clientBytes.Length;)
        {
            bool isHeader = clientBytes[at] == (byte)'A';
            int size = isHeader ? 8 : (int)BinaryPrimitives.ReadUInt32BigEndian(clientBytes.AsSpan(at));
            if (!isHeader)
            {
                result.Add(clientBytes[at..(at + size)]);
            }

            at += size;
        }

        return [.. result.Where(frame => frame[5] == Amqp1ReferenceFrame.AmqpType)];
    }

    internal static ulong?[] Descriptors(Amqp1ReferenceContainer container) => [.. container.Frames.Select(frame => frame.Body?.Descriptor)];

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.3.2-1")]
    [SpecRequirement("Amqp1:Core-2.4.1-1")]
    [SpecRequirement("Amqp1:Core-2.4.3-1")]
    [SpecRequirement("Amqp1:Core-2.5.1-1")]
    [SpecRequirement("Amqp1:Core-2.5.1-2")]
    [SpecRequirement("Amqp1:Core-2.6.3-1")]
    [SpecRequirement("Amqp1:Core-2.6.6-1")]
    public Task Order_SendMessage_OpenBeginAttachTransferDetachEndClose() =>
        Known.RunCaseAsync(["Amqp1:Core-2.3.2-1", "Amqp1:Core-2.4.1-1", "Amqp1:Core-2.4.3-1", "Amqp1:Core-2.5.1-1", "Amqp1:Core-2.5.1-2", "Amqp1:Core-2.6.3-1", "Amqp1:Core-2.6.6-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { LinkBehaviour = new AcceptingLink() });

            CurlRunResult result = await RunAsync(container, "-d", "hi");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsEmpty(result.StandardError);
            Assert.IsEmpty(result.StandardOutput);
            CollectionAssert.AreEqual(
                new ulong?[]
                {
                    Amqp1ReferenceDescriptors.SaslInit, Amqp1ReferenceDescriptors.Open, Amqp1ReferenceDescriptors.Begin, Amqp1ReferenceDescriptors.Attach,
                    Amqp1ReferenceDescriptors.Transfer, Amqp1ReferenceDescriptors.Detach, Amqp1ReferenceDescriptors.End, Amqp1ReferenceDescriptors.Close,
                },
                Descriptors(container));
            Assert.IsTrue(container.Frames.All(frame => frame.Channel == 0));
            Assert.IsNull(container.Frames[2].Body!.Field(0), "an initiating begin carries no remote-channel");
            Amqp1ReferenceDescribed attach = container.Frames[3].Body!;
            Assert.AreEqual("curl", attach.Field(0));
            Assert.AreEqual(0u, attach.Field(1));
            Assert.IsFalse((bool)attach.Field(2)!, "a link that sends is role sender (false)");
            Assert.AreEqual(0u, container.Frames[4].Body!.Field(0), "the transfer uses the attached handle");
            Amqp1ReferenceDescribed detach = container.Frames[5].Body!;
            Assert.AreEqual(0u, detach.Field(0));
            Assert.IsTrue((bool)detach.Field(1)!, "the detach closes the link");
            Assert.IsNull(container.Frames[7].Body!.Field(0), "a close without error");
            List<byte[]> frames = ClientAmqpFrames(result.Exchanges[0].ClientBytes);
            Assert.HasCount(7, frames);
            Assert.AreEqual(Amqp1ReferenceDescriptors.Close, (ulong)frames[^1][10], "the close is the last frame curl writes");
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.6.3-2")]
    [SpecRequirement("Amqp1:Core-2.8.15-1")]
    public Task Link_RefusedByDetachWithError_SendsNoTransferAndReportsTheError() =>
        Known.RunCaseAsync(["Amqp1:Core-2.6.3-2", "Amqp1:Core-2.8.15-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions());

            CurlRunResult result = await RunAsync(container, "-d", "hi");

            Assert.AreEqual(78, result.ExitCode);
            Assert.AreEqual(RefusedLinkError, Text(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
            Assert.DoesNotContain((ulong?)Amqp1ReferenceDescriptors.Transfer, Descriptors(container));
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.6.5-1")]
    public Task Link_DetachedByThePeer_AnswersWithADetach() =>
        Known.RunCaseAsync(["Amqp1:Core-2.6.5-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions());

            CurlRunResult result = await RunAsync(container, "-d", "hi");

            Assert.AreEqual(78, result.ExitCode);
            CollectionAssert.Contains(Descriptors(container), (ulong?)Amqp1ReferenceDescriptors.Detach, "curl answers the container's closing detach with its own");
        });

    /// <summary>
    /// A link that grants the sender credit once attached, after first sending <paramref name="emptyFrames"/> empty
    /// frames, and accepts each complete delivery after <paramref name="dispositionDelay"/>.
    /// </summary>
    internal sealed class AcceptingLink(int emptyFrames = 0, TimeSpan dispositionDelay = default) : IAmqp1ReferenceLinkBehaviour
    {
        public async ValueTask AttachedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame attach)
        {
            for (int sent = 0; sent < emptyFrames; sent++)
            {
                await session.SendEmptyFrameAsync();
            }

            await session.SendAsync(attach.Channel, new Amqp1ReferenceDescribed(Amqp1ReferenceDescriptors.Flow, new object?[] { 0u, 2048u, 0u, 2048u, 0u, 0u, 10u }));
        }

        public async ValueTask ReceivedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame)
        {
            if (frame.Body is { Descriptor: Amqp1ReferenceDescriptors.Transfer } transfer && !true.Equals(transfer.Field(5)))
            {
                await Task.Delay(dispositionDelay);
                await session.SendAsync(frame.Channel, new Amqp1ReferenceDescribed(Amqp1ReferenceDescriptors.Disposition,
                    new object?[] { true, transfer.Field(1), null, true, new Amqp1ReferenceDescribed(Accepted, Array.Empty<object?>()) }));
            }
        }
    }
}
