using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1ProtocolHeaderCases;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1SessionLinkCases;

namespace Curl.Conformance.Extended.UnitTests.Amqp1;

/// <summary>Part 2 sections 2.3, 2.4 and 2.8: frame size and its negotiation, empty frames and the idle timeout, and a close carrying an error.</summary>
[TestClass]
public sealed class Amqp1ConnectionCases
{
    private const string CloseCondition = "amqp:internal-error";

    private const string CloseDescription = "the reference container closes";

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.3.1-1")]
    [SpecRequirement("Amqp1:Core-2.3.1-2")]
    [SpecRequirement("Amqp1:Core-2.4.1-2")]
    [SpecRequirement("Amqp1:Core-2.4.1-3")]
    public Task FrameSize_ContainerOffers512_EveryFrameFitsAndTheMessageIsSplit() =>
        Known.RunCaseAsync(["Amqp1:Core-2.3.1-1", "Amqp1:Core-2.3.1-2", "Amqp1:Core-2.4.1-2", "Amqp1:Core-2.4.1-3"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { MaxFrameSize = 512, LinkBehaviour = new AcceptingLink() });

            CurlRunResult result = await RunAsync(container, "-d", new string('x', 1500));

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsEmpty(result.StandardError);
            Assert.IsEmpty(result.StandardOutput);
            uint offered = (uint)container.Frames[1].Body!.Field(2)!;
            Assert.IsTrue(offered >= 512, $"curl's open offers max-frame-size {offered}, below the 512 minimum");
            List<byte[]> frames = ClientAmqpFrames(result.Exchanges[0].ClientBytes);
            Assert.IsTrue(frames.All(frame => frame.Length <= 512), "a frame is larger than the container's max-frame-size");
            Assert.IsTrue(frames.All(frame => frame.Length >= 8 && frame[4] >= 2), "a frame is shorter than its header or its doff is below 2");
            Assert.IsGreaterThan(3, container.Frames.Count(frame => frame.Body?.Descriptor == Amqp1ReferenceDescriptors.Transfer));
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.3.2-2")]
    public Task EmptyFrames_ContainerSendsTwoBeforeFlow_CurlIgnoresThem() =>
        Known.RunCaseAsync(["Amqp1:Core-2.3.2-2"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { LinkBehaviour = new AcceptingLink(emptyFrames: 2) });

            CurlRunResult result = await RunAsync(container, "-d", "hi");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.IsEmpty(result.StandardError);
            Assert.AreEqual((ulong?)Amqp1ReferenceDescriptors.Close, Descriptors(container)[^1]);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.4.5-1")]
    public Task IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames() =>
        Known.RunCaseAsync(["Amqp1:Core-2.4.5-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions
            {
                IdleTimeOut = 1000,
                LinkBehaviour = new AcceptingLink(dispositionDelay: TimeSpan.FromMilliseconds(2500)),
            });

            CurlRunResult result = await RunAsync(container, "-d", "hi");

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            ulong?[] descriptors = Descriptors(container);
            int transfer = Array.IndexOf(descriptors, Amqp1ReferenceDescriptors.Transfer);
            Assert.IsGreaterThanOrEqualTo(2, descriptors.Skip(transfer).Count(descriptor => descriptor is null), "curl sent no empty frames while idle past half the container's idle-time-out");
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.8.15-1")]
    public Task CloseWithError_ContainerClosesAfterAttach_ReportsTheErrorWithExit8() =>
        Known.RunCaseAsync(["Amqp1:Core-2.8.15-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { LinkBehaviour = new ClosingLink() });

            CurlRunResult result = await RunAsync(container, "-d", "hi");

            Assert.AreEqual(8, result.ExitCode);
            Assert.AreEqual($"curl: (8) AMQP 1.0 close error: {CloseCondition}: {CloseDescription}" + Environment.NewLine, Text(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
            Assert.DoesNotContain((ulong?)Amqp1ReferenceDescriptors.Transfer, Descriptors(container));
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.4.3-2")]
    public Task CloseWithError_ContainerClosesAfterAttach_CurlAnswersWithClose() =>
        Known.RunCaseAsync(["Amqp1:Core-2.4.3-2"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { LinkBehaviour = new ClosingLink() });

            CurlRunResult result = await RunAsync(container, "-d", "hi");

            Assert.AreEqual(8, result.ExitCode);
            Assert.AreEqual((ulong?)Amqp1ReferenceDescriptors.Close, Descriptors(container)[^1], "curl answers the container's close with its own");
        });

    /// <summary>A link the container answers by closing the connection with an error.</summary>
    private sealed class ClosingLink : IAmqp1ReferenceLinkBehaviour
    {
        public ValueTask AttachedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame attach)
        {
            Amqp1ReferenceDescribed error = new(Amqp1ReferenceDescriptors.Error, new object?[] { new Amqp1ReferenceSymbol(CloseCondition), CloseDescription });
            return session.SendAsync(0, new Amqp1ReferenceDescribed(Amqp1ReferenceDescriptors.Close, new object?[] { error }));
        }

        public ValueTask ReceivedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame) => ValueTask.CompletedTask;
    }
}
