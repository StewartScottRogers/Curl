using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Harness;
using static Curl.Conformance.Extended.UnitTests.Amqp1.Amqp1ProtocolHeaderCases;

namespace Curl.Conformance.Extended.UnitTests.Amqp1;

/// <summary>Part 5 section 5.3: sasl-init with PLAIN from <c>-u</c>, ANONYMOUS without it, and outcomes other than ok.</summary>
[TestClass]
public sealed class Amqp1SaslCases
{
    private static Task<CurlRunResult> RunAsync(Amqp1ReferenceContainer container, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(CurlRunner, [ReferenceRoute.Tcp(5672, container)], ["-sS", "-m", "10", .. arguments, "amqp1://localhost/queue"]);

    private static Amqp1ReferenceDescribed SaslInit(Amqp1ReferenceContainer container) => container.Frames[0].Body!;

    [TestMethod]
    [SpecRequirement("Amqp1:Sasl-5.3.2-1")]
    [SpecRequirement("Amqp1:Sasl-5.3.3.2-1")]
    public Task Sasl_UserGiven_InitsPlainWithItsCredentials() =>
        Known.RunCaseAsync(["Amqp1:Sasl-5.3.2-1", "Amqp1:Sasl-5.3.3.2-1"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions());

            CurlRunResult result = await RunAsync(container, "-u", "alice:secret");

            Assert.AreEqual(new Amqp1ReferenceSymbol("PLAIN"), SaslInit(container).Field(0));
            CollectionAssert.AreEqual("\0alice\0secret"u8.ToArray(), (byte[])SaslInit(container).Field(1)!);
            Assert.AreEqual("localhost", SaslInit(container).Field(2));
            Assert.AreEqual(RefusedLinkError, Text(result.StandardError));
            Assert.AreEqual(78, result.ExitCode);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Sasl-5.3.2-1")]
    [SpecRequirement("Amqp1:Sasl-5.3.3.2-2")]
    public Task Sasl_NoUser_InitsAnonymous() =>
        Known.RunCaseAsync(["Amqp1:Sasl-5.3.2-1", "Amqp1:Sasl-5.3.3.2-2"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { SaslMechanisms = ["ANONYMOUS"] });

            CurlRunResult result = await RunAsync(container);

            Assert.AreEqual(new Amqp1ReferenceSymbol("ANONYMOUS"), SaslInit(container).Field(0));
            Assert.AreEqual(RefusedLinkError, Text(result.StandardError));
            Assert.AreEqual(78, result.ExitCode);
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Sasl-5.3.2-2")]
    public Task Sasl_MechanismNotOffered_FailsWithLoginDeniedBeforeInit() =>
        Known.RunCaseAsync(["Amqp1:Sasl-5.3.2-2"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions { SaslMechanisms = ["PLAIN"] });

            CurlRunResult result = await RunAsync(container);

            Assert.AreEqual(67, result.ExitCode);
            Assert.IsEmpty(container.Frames);
            Assert.AreEqual("curl: (67) AMQP 1.0 server does not offer the SASL ANONYMOUS mechanism" + Environment.NewLine, Text(result.StandardError));
        });

    [TestMethod]
    [SpecRequirement("Amqp1:Sasl-5.3.3.6-2")]
    public Task Sasl_OutcomeAuth_FailsWithLoginDeniedAndNoAmqpHeader() =>
        Known.RunCaseAsync(["Amqp1:Sasl-5.3.3.6-2"], async () =>
        {
            Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions());

            CurlRunResult result = await RunAsync(container, "-u", "alice:wrong");

            Assert.AreEqual(67, result.ExitCode);
            Assert.HasCount(1, container.Headers);
            Assert.HasCount(1, container.Frames);
            Assert.AreEqual("curl: (67) AMQP 1.0 SASL authentication failed with outcome code 1" + Environment.NewLine, Text(result.StandardError));
            Assert.IsEmpty(result.StandardOutput);
        });
}
