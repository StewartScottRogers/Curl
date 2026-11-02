using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Amqp1;

/// <summary>Part 2 section 2.2: the protocol headers curl sends over amqp1:// and amqp1s://, and a container answering with another one.</summary>
[TestClass]
public sealed class Amqp1ProtocolHeaderCases
{
    internal static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    internal static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly X509Certificate2 Certificate = ReferenceCertificate.CreateSelfSigned("localhost");

    /// <summary>The standard error of a link refused by <see cref="Amqp1ReferenceLinkRefusal"/>.</summary>
    internal static string RefusedLinkError =>
        $"curl: (78) AMQP 1.0 detach error: {Amqp1ReferenceLinkRefusal.Condition}: {Amqp1ReferenceLinkRefusal.Description}" + Environment.NewLine;

    internal static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.2-1")]
    [SpecRequirement("Amqp1:Core-2.2-2")]
    [SpecRequirement("Amqp1:Core-2.2-3")]
    [SpecRequirement("Amqp1:Sasl-5.3.1-1")]
    [SpecRequirement("Amqp1:Sasl-5.3.3.6-1")]
    public Task Headers_Amqp1_SaslHeaderFirstThenAmqpHeaderAfterOutcome() =>
        Known.RunCaseAsync(["Amqp1:Core-2.2-1", "Amqp1:Core-2.2-2", "Amqp1:Core-2.2-3", "Amqp1:Sasl-5.3.1-1", "Amqp1:Sasl-5.3.3.6-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions());

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(5672, container)], ["-sS", "-m", "10", "amqp1://localhost/queue"]);

            AssertHeadersAndRefusedLink(container, result);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.2-1")]
    [SpecRequirement("Amqp1:Sasl-5.3.1-1")]
    public Task Headers_Amqp1sOverTls_SameHeadersInsideTls() =>
        Known.RunCaseAsync(["Amqp1:Core-2.2-1", "Amqp1:Sasl-5.3.1-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions());

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tls(5671, container, Certificate)], ["-sS", "-k", "-m", "10", "amqp1s://localhost/queue", "-d", "hi"]);

            AssertHeadersAndRefusedLink(container, result);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Core-2.2-4")]
    public Task Headers_ServerAnswersAnotherHeader_ClosesWithWeirdServerReply() =>
        Known.RunCaseAsync(["Amqp1:Core-2.2-4"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { AnswerHeaderProtocolId = Amqp1ReferenceContainer.AmqpProtocolId });

            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(5672, container)], ["-sS", "-m", "10", "amqp1://localhost/queue"]);

            Assert.AreEqual(8, result.ExitCode);
            CollectionAssert.AreEqual(Amqp1ReferenceSession.Header(Amqp1ReferenceContainer.SaslProtocolId), result.Exchanges[0].ClientBytes);
            CollectionAssert.AreEqual(Amqp1ReferenceSession.Header(Amqp1ReferenceContainer.AmqpProtocolId), result.Exchanges[0].ServerBytes);
            Assert.AreEqual("curl: (8) Weird server reply: malformed AMQP 1.0 reply: The peer answered with protocol header AMQP 0 1.0.0 to AMQP 3 1.0.0." + Environment.NewLine, Text(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
        });

    private static void AssertHeadersAndRefusedLink(Amqp1ReferenceContainer container, CurlRunResult result)
    {
        Assert.AreEqual(78, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual(RefusedLinkError, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        CollectionAssert.AreEqual(Amqp1ReferenceSession.Header(Amqp1ReferenceContainer.SaslProtocolId), result.Exchanges[0].ClientBytes[..8]);
        Assert.HasCount(2, container.Headers);
        CollectionAssert.AreEqual(Amqp1ReferenceSession.Header(Amqp1ReferenceContainer.AmqpProtocolId), container.Headers[1]);
        ulong[] descriptors = [.. container.Frames.Take(4).Select(frame => frame.Body!.Descriptor)];
        CollectionAssert.AreEqual(
            new[] { Amqp1ReferenceDescriptors.SaslInit, Amqp1ReferenceDescriptors.Open, Amqp1ReferenceDescriptors.Begin, Amqp1ReferenceDescriptors.Attach },
            descriptors);
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0 }, container.Frames.Take(4).Select(frame => frame.Type).ToArray());
    }
}
