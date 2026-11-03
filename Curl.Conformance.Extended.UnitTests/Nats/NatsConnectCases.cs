using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Nats;

/// <summary>
/// Checks curl's INFO handling and CONNECT line against the NATS client protocol (BL-4487): the fields CONNECT carries
/// with no credentials, user and password, or a token; INFO split across reads; <c>tls_required</c>; and <c>-ERR</c>
/// at connect.
/// </summary>
[TestClass]
public sealed class NatsConnectCases
{
    private static readonly ScriptStep[] PublishAnswered =
    [
        ScriptStep.Send("INFO {\"server_id\":\"ref\",\"max_payload\":1048576}\r\n"),
        ScriptStep.ReceiveUntil("PING\r\n"),
        ScriptStep.Send("PONG\r\n"),
    ];

    [TestMethod]
    [SpecRequirement("Nats:NATS-INFO-1")]
    [SpecRequirement("Nats:NATS-CONNECT-1")]
    [SpecRequirement("Nats:NATS-CONNECT-2")]
    [SpecRequirement("Nats:NATS-CONNECT-3")]
    [SpecRequirement("Nats:NATS-CONNECT-4")]
    [SpecRequirement("Nats:NATS-CONNECT-6")]
    [SpecRequirement("Nats:NATS-HMSG-1")]
    [SpecRequirement("Nats:NATS-PROTO-1")]
    public async Task Publish_NoCredentials_SendsConnectWithEveryRequiredFieldAndNoAuthField()
    {
        CurlRunResult result = await NatsRun.RunAsync(PublishAnswered, "-d", "hi", "nats://localhost/greet");

        NatsRun.AssertExit(result, 0, string.Empty);
        Assert.AreEqual(NatsRun.PlainConnect + "PUB greet 2\r\nhi\r\nPING\r\n", NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-CONNECT-5")]
    public async Task Publish_UserAndPassword_SendsThemAsUserAndPass()
    {
        CurlRunResult result = await NatsRun.RunAsync(PublishAnswered, "-u", "alice:s3cret", "-d", "hi", "nats://localhost/greet");

        NatsRun.AssertExit(result, 0, string.Empty);
        StringAssert.StartsWith(
            NatsRun.ClientText(result),
            "CONNECT {\"verbose\":false,\"pedantic\":false,\"tls_required\":false,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1,\"user\":\"alice\",\"pass\":\"s3cret\"}\r\n");
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-CONNECT-5")]
    public async Task Publish_TokenWithEmptyPassword_SendsItAsAuthToken()
    {
        CurlRunResult result = await NatsRun.RunAsync(PublishAnswered, "-u", "t0ken:", "-d", "hi", "nats://localhost/greet");

        NatsRun.AssertExit(result, 0, string.Empty);
        StringAssert.StartsWith(
            NatsRun.ClientText(result),
            "CONNECT {\"verbose\":false,\"pedantic\":false,\"tls_required\":false,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1,\"auth_token\":\"t0ken\"}\r\n");
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-INFO-2")]
    [SpecRequirement("Nats:NATS-PROTO-2")]
    public async Task Publish_InfoSplitAcrossReads_HonoursItsMaxPayload()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {\"server_id\":\"ref\",\"max_pay"), ScriptStep.Send("load\":4}\r"), ScriptStep.Send("\n")],
            "-d",
            "hello",
            "nats://localhost/greet");

        NatsRun.AssertExit(result, 63, "curl: (63) NATS payload of 5 bytes is larger than the server's max_payload of 4\n");
        Assert.IsFalse(NatsRun.ClientText(result).Contains("PUB ", StringComparison.Ordinal));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-INFO-3")]
    public async Task Subscribe_TlsRequiredOverPlainNats_IsRefusedBeforeConnect()
    {
        CurlRunResult result = await NatsRun.RunAsync([ScriptStep.Send("INFO {\"tls_required\":true}\r\n")], "nats://localhost/greet");

        NatsRun.AssertExit(result, 35, "curl: (35) NATS server requires TLS; use natss://\n");
        Assert.AreEqual(string.Empty, NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-INFO-3")]
    [SpecRequirement("Nats:NATS-CONNECT-7")]
    public async Task Publish_Natss_SpeaksTlsFromTheFirstByteAndSaysTlsRequired()
    {
        CurlRunResult result = await NatsRun.RunTlsAsync(
            [ScriptStep.Send("INFO {\"tls_required\":true}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\n")],
            "-d",
            "hi",
            "natss://localhost/greet");

        NatsRun.AssertExit(result, 0, string.Empty);
        Assert.AreEqual(
            "CONNECT {\"verbose\":false,\"pedantic\":false,\"tls_required\":true,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1}\r\nPUB greet 2\r\nhi\r\nPING\r\n",
            NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-INFO-4")]
    [SpecRequirement("Nats:NATS-ERR-1")]
    public async Task Subscribe_AuthRequiredAndErrAtConnect_FailsWithLoginDenied()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {\"auth_required\":true}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("-ERR 'Authorization Violation'\r\n")],
            "nats://localhost/greet");

        NatsRun.AssertExit(result, 67, "curl: (67) NATS server refused the login: Authorization Violation\n");
        Assert.AreEqual(NatsRun.PlainConnect + "SUB greet 1\r\nPING\r\n", NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-OK-1")]
    [SpecRequirement("Nats:NATS-INFO-5")]
    public async Task Publish_OkAndInfoBeforePong_AreAccepted()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("+OK\r\nINFO {\"connect_urls\":[]}\r\nPONG\r\n")],
            "-d",
            "hi",
            "nats://localhost/greet");

        NatsRun.AssertExit(result, 0, string.Empty);
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-INFO-1")]
    public async Task Subscribe_FirstLineNotInfo_IsAWeirdServerReply()
    {
        CurlRunResult result = await NatsRun.RunAsync([ScriptStep.Send("PONG\r\n")], "nats://localhost/greet");

        NatsRun.AssertExit(result, 8, "curl: (8) NATS server did not begin with INFO\n");
    }
}
