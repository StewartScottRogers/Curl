using System.Text;
using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Amqp1.Messaging;
using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1ProtocolHeaderCases;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1SessionLinkCases;

namespace Curl.Conformance.Extended.UnitTests.Amqp1.Messaging;

/// <summary>
/// Part 2 sections 2.6.7 to 2.7 and Part 3 sections 3.2 to 3.5 for <c>-d</c>: curl waits for link credit, sends the
/// body as one data section, splits it over transfers with <c>more</c>, and ends by the outcome the container settles with.
/// </summary>
[TestClass]
public sealed class Amqp1SendCases
{
    internal static Amqp1ReferenceDescribed Outcome(ulong descriptor, params object?[] fields) => new(descriptor, fields);

    internal static Amqp1ReferenceDescribed[] Transfers(Amqp1ReferenceContainer container) =>
        [.. container.Frames.Where(frame => frame.Body?.Descriptor == Amqp1ReferenceDescriptors.Transfer).Select(frame => frame.Body!)];

    internal static byte[] Payload(Amqp1ReferenceContainer container) =>
        [.. container.Frames.Where(frame => frame.Body?.Descriptor == Amqp1ReferenceDescriptors.Transfer).SelectMany(frame => frame.Payload)];

    private static async Task<(Amqp1ReferenceContainer Container, CurlRunResult Result)> SendAsync(Amqp1ReferenceDescribed outcome, uint? maxFrameSize = null, string body = "hi", int creditDelayMilliseconds = 0)
    {
        Amqp1ReferenceReceivingLink link = new(outcome, TimeSpan.FromMilliseconds(creditDelayMilliseconds));
        Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { LinkBehaviour = link, MaxFrameSize = maxFrameSize });
        CurlRunResult result = await RunAsync(container, "-d", body);
        await link.Credit;
        Assert.AreEqual(0, link.TransfersWithoutCredit, "no transfer goes out while the link has no credit");
        return (container, result);
    }

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.6.7-1")]
    public Task Credit_ContainerGrantsNoneFor300ms_CurlWaitsThenSends() =>
        Known.RunCaseAsync(["Amqp1:Core-2.6.7-1"], async () =>
        {
            var (container, result) = await SendAsync(Outcome(Amqp1ReferenceMessage.Accepted), creditDelayMilliseconds: 300);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsEmpty(result.StandardError);
            Assert.HasCount(1, Transfers(container));
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.6.12-1")]
    [SpecRequirement("Amqp1:Messaging-3.2-1")]
    [SpecRequirement("Amqp1:Messaging-3.4.2-1")]
    [SpecRequirement("Amqp1:Messaging-3.5.4-1")]
    public Task Send_SettledAccepted_OneDataSectionAndExit0() =>
        Known.RunCaseAsync(["Amqp1:Core-2.6.12-1", "Amqp1:Messaging-3.2-1", "Amqp1:Messaging-3.4.2-1", "Amqp1:Messaging-3.5.4-1"], async () =>
        {
            var (container, result) = await SendAsync(Outcome(Amqp1ReferenceMessage.Accepted));

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsEmpty(result.StandardError);
            Assert.IsEmpty(result.StandardOutput);
            Amqp1ReferenceDescribed attach = container.Frames.First(frame => frame.Body?.Descriptor == Amqp1ReferenceDescriptors.Attach).Body!;
            Assert.AreEqual("queue", ((Amqp1ReferenceDescribed)attach.Field(6)!).Field(0), "the target names the address");
            Amqp1ReferenceDescribed transfer = Transfers(container).Single();
            Assert.AreEqual(0u, transfer.Field(0));
            Assert.IsNotNull(transfer.Field(1), "the first transfer of a delivery carries its delivery-id");
            Assert.IsInstanceOfType<byte[]>(transfer.Field(2), "the first transfer of a delivery carries its delivery-tag");
            Amqp1ReferenceDescribed section = Amqp1ReferenceMessage.Decode(Payload(container)).Single();
            Assert.AreEqual(Amqp1ReferenceMessage.Data, section.Descriptor);
            CollectionAssert.AreEqual("hi"u8.ToArray(), (byte[])section.Value!);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Messaging-3.4.3-1")]
    public Task Send_Rejected_FailsWithExit55AndTheError() =>
        Known.RunCaseAsync(["Amqp1:Messaging-3.4.3-1"], async () =>
        {
            Amqp1ReferenceDescribed error = new(Amqp1ReferenceDescriptors.Error, new object?[] { new Amqp1ReferenceSymbol("amqp:invalid-field"), "no such field" });

            var (_, result) = await SendAsync(Outcome(Amqp1ReferenceMessage.Rejected, error));

            Assert.AreEqual(55, result.ExitCode);
            Assert.AreEqual("curl: (55) AMQP 1.0 message rejected: amqp:invalid-field: no such field" + Environment.NewLine, Text(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Messaging-3.4.4-1")]
    public Task Send_Released_FailsWithExit55() =>
        Known.RunCaseAsync(["Amqp1:Messaging-3.4.4-1"], async () =>
        {
            var (_, result) = await SendAsync(Outcome(Amqp1ReferenceMessage.Released));

            Assert.AreEqual(55, result.ExitCode);
            Assert.AreEqual("curl: (55) AMQP 1.0 message released" + Environment.NewLine, Text(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Messaging-3.4.5-1")]
    public Task Send_Modified_FailsWithExit55() =>
        Known.RunCaseAsync(["Amqp1:Messaging-3.4.5-1"], async () =>
        {
            var (_, result) = await SendAsync(Outcome(Amqp1ReferenceMessage.Modified, true, true));

            Assert.AreEqual(55, result.ExitCode);
            Assert.AreEqual("curl: (55) AMQP 1.0 message modified" + Environment.NewLine, Text(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.7.5-1")]
    public Task Send_1500BytesAt512PerFrame_SplitWithMoreOnAllButTheLast() =>
        Known.RunCaseAsync(["Amqp1:Core-2.7.5-1"], async () =>
        {
            string body = new('x', 1500);

            var (container, result) = await SendAsync(Outcome(Amqp1ReferenceMessage.Accepted), maxFrameSize: 512, body: body);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Amqp1ReferenceDescribed[] transfers = Transfers(container);
            Assert.IsGreaterThanOrEqualTo(3, transfers.Length);
            Assert.IsTrue(transfers[..^1].All(transfer => true.Equals(transfer.Field(5))), "every transfer but the last carries more");
            Assert.IsFalse(true.Equals(transfers[^1].Field(5)), "the last transfer carries no more");
            Assert.IsTrue(transfers.All(transfer => transfer.Field(1) is null || transfer.Field(1)!.Equals(transfers[0].Field(1))), "a continuation keeps the delivery-id");
            Assert.IsTrue(transfers.All(transfer => 0u.Equals(transfer.Field(0))), "every transfer is on the attached handle");
            Amqp1ReferenceDescribed section = Amqp1ReferenceMessage.Decode(Payload(container)).Single();
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(body), (byte[])section.Value!);
        });
}
