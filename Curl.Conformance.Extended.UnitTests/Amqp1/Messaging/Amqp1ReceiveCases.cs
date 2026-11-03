using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Amqp1.Messaging;
using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1ProtocolHeaderCases;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1SessionLinkCases;

namespace Curl.Conformance.Extended.UnitTests.Amqp1.Messaging;

/// <summary>
/// Part 2 sections 2.6.7 to 2.7 and Part 3 sections 3.2 to 3.5 without <c>-d</c>: curl grants credit, reads the
/// message's body sections to standard output, settles an unsettled delivery accepted, and acts on transfers the
/// container gets wrong.
/// </summary>
[TestClass]
public sealed class Amqp1ReceiveCases
{
    private static Amqp1ReferenceDescribed Section(ulong descriptor, object? value) => new(descriptor, value);

    private static async Task<(Amqp1ReferenceContainer Container, CurlRunResult Result)> ReceiveAsync(Amqp1ReferenceDelivery[] transfers, params string[] arguments)
    {
        Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { LinkBehaviour = new Amqp1ReferenceSendingLink(transfers) });
        return (container, await RunAsync(container, arguments));
    }

    private static Amqp1ReferenceDescribed[] Performatives(Amqp1ReferenceContainer container, ulong descriptor) =>
        [.. container.Frames.Where(frame => frame.Body?.Descriptor == descriptor).Select(frame => frame.Body!)];

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.6.7-2")]
    [SpecRequirement("Amqp1:Core-2.7.6-1")]
    [SpecRequirement("Amqp1:Messaging-3.2.6-1")]
    [SpecRequirement("Amqp1:Messaging-3.5.3-1")]
    public Task Receive_UnsettledData_WritesItAndSettlesAccepted() =>
        Known.RunCaseAsync(["Amqp1:Core-2.6.7-2", "Amqp1:Core-2.7.6-1", "Amqp1:Messaging-3.2.6-1", "Amqp1:Messaging-3.5.3-1"], async () =>
        {
            byte[] message = Amqp1ReferenceMessage.Encode(Section(Amqp1ReferenceMessage.Data, "hello"u8.ToArray()));

            var (container, result) = await ReceiveAsync([new(Amqp1ReferenceMessage.Transfer(0, 0, settled: false, more: false), message)]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsEmpty(result.StandardError);
            Assert.AreEqual("hello", Text(result.StandardOutput));
            Amqp1ReferenceDescribed attach = Performatives(container, Amqp1ReferenceDescriptors.Attach).Single();
            Assert.AreEqual("queue", ((Amqp1ReferenceDescribed)attach.Field(5)!).Field(0), "the source names the address");
            Amqp1ReferenceDescribed flow = Performatives(container, Amqp1ReferenceDescriptors.Flow).Single();
            Assert.AreEqual(0u, flow.Field(4), "the flow names the attached handle");
            Assert.IsGreaterThan(0u, (uint)flow.Field(6)!, "the flow grants link credit");
            Amqp1ReferenceDescribed disposition = Performatives(container, Amqp1ReferenceDescriptors.Disposition).Single();
            Assert.IsTrue((bool)disposition.Field(0)!, "the disposition comes from the receiver");
            Assert.AreEqual(0u, disposition.Field(1), "the disposition names the delivery-id");
            Assert.IsTrue((bool)disposition.Field(3)!, "the receiver settles the delivery");
            Assert.AreEqual(Amqp1ReferenceMessage.Accepted, ((Amqp1ReferenceDescribed)disposition.Field(4)!).Descriptor);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Messaging-3.2.8-1")]
    public Task Receive_AmqpValueString_WritesTheString() =>
        Known.RunCaseAsync(["Amqp1:Messaging-3.2.8-1"], async () =>
        {
            byte[] message = Amqp1ReferenceMessage.Encode(Section(Amqp1ReferenceMessage.AmqpValue, "a value"));

            var (_, result) = await ReceiveAsync([new(Amqp1ReferenceMessage.Transfer(0, 0, settled: false, more: false), message)]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsEmpty(result.StandardError);
            Assert.AreEqual("a value", Text(result.StandardOutput));
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.6.12-2")]
    [SpecRequirement("Amqp1:Core-2.7.5-1")]
    [SpecRequirement("Amqp1:Messaging-3.2-2")]
    public Task Receive_SettledTwoDataSectionsInTwoTransfersWithBareMessageAround_WritesBothAndSendsNoDisposition() =>
        Known.RunCaseAsync(["Amqp1:Core-2.6.12-2", "Amqp1:Core-2.7.5-1", "Amqp1:Messaging-3.2-2"], async () =>
        {
            // application-properties is a map8 {"k": "v"} and the footer an empty map8, written by hand: the
            // container's encoder writes no maps.
            byte[] message =
            [
                .. Amqp1ReferenceMessage.Encode(
                    Section(Amqp1ReferenceMessage.Header, new object?[] { true }),
                    Section(Amqp1ReferenceMessage.Properties, new object?[] { "id-1" })),
                0x00, 0x53, (byte)Amqp1ReferenceMessage.ApplicationProperties, 0xc1, 0x07, 0x02, 0xa1, 0x01, (byte)'k', 0xa1, 0x01, (byte)'v',
                .. Amqp1ReferenceMessage.Encode(
                    Section(Amqp1ReferenceMessage.Data, "one"u8.ToArray()),
                    Section(Amqp1ReferenceMessage.Data, "two"u8.ToArray())),
                0x00, 0x53, (byte)Amqp1ReferenceMessage.Footer, 0xc1, 0x01, 0x00,
            ];
            int half = message.Length / 2;

            var (container, result) = await ReceiveAsync(
            [
                new(Amqp1ReferenceMessage.Transfer(0, 0, settled: true, more: true), message[..half]),
                new(Amqp1ReferenceMessage.Transfer(0, 0, settled: true, more: false), message[half..]),
            ]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsEmpty(result.StandardError);
            Assert.AreEqual("onetwo", Text(result.StandardOutput));
            Assert.IsEmpty(Performatives(container, Amqp1ReferenceDescriptors.Disposition), "a delivery the sender settled is not settled again");
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.6.7-3")]
    public Task Receive_NoMessage_TimesOutWithExit28AndWritesNothing() =>
        Known.RunCaseAsync(["Amqp1:Core-2.6.7-3"], async () =>
        {
            var (container, result) = await ReceiveAsync([], "-m", "1");

            Assert.AreEqual(28, result.ExitCode);
            Assert.StartsWith("curl: (28) ", Text(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
            Assert.IsEmpty(Performatives(container, Amqp1ReferenceDescriptors.Disposition));
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.7.5-2")]
    public Task Receive_TransferOnUnattachedHandle_EndsTheSessionWithUnattachedHandle() =>
        Known.RunCaseAsync(["Amqp1:Core-2.7.5-2"], async () =>
        {
            byte[] message = Amqp1ReferenceMessage.Encode(Section(Amqp1ReferenceMessage.Data, "hello"u8.ToArray()));

            var (container, result) = await ReceiveAsync([new(Amqp1ReferenceMessage.Transfer(7, 0, settled: false, more: false), message)]);

            Assert.AreEqual(8, result.ExitCode);
            Assert.IsEmpty(result.StandardOutput);
            Amqp1ReferenceDescribed end = Performatives(container, Amqp1ReferenceDescriptors.End).Single();
            Assert.AreEqual(new Amqp1ReferenceSymbol("amqp:session:unattached-handle"), ((Amqp1ReferenceDescribed)end.Field(0)!).Field(0));
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.7.5-3")]
    public Task Receive_ContinuationChangesTheDeliveryId_FailsWithExit8() =>
        Known.RunCaseAsync(["Amqp1:Core-2.7.5-3"], async () =>
        {
            byte[] message = Amqp1ReferenceMessage.Encode(Section(Amqp1ReferenceMessage.Data, "hello"u8.ToArray()));

            var (_, result) = await ReceiveAsync(
            [
                new(Amqp1ReferenceMessage.Transfer(0, 0, settled: false, more: true), message[..4]),
                new(Amqp1ReferenceMessage.Transfer(0, 1, settled: false, more: false), message[4..]),
            ]);

            Assert.AreEqual(8, result.ExitCode);
            Assert.IsEmpty(result.StandardOutput);
        });
}
