using System.Text.RegularExpressions;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Nats;

/// <summary>
/// Checks curl's PUB, request/reply and subject rules against the NATS client protocol (BL-4487): CRLF framing,
/// <c>-ERR</c> after PUB, a request's unique inbox, and subjects with a wildcard or an empty token refused.
/// </summary>
[TestClass]
public sealed partial class NatsPublishCases
{
    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("Nats:NATS-PUB-1")]
    [SpecRequirement("Nats:NATS-PING-2")]
    public async Task Publish_PayloadWithCrLf_IsFramedByItsByteCount()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\n")],
            "--data-binary",
            "a\r\nb",
            "nats://localhost/orders.new");

        NatsRun.AssertExit(result, 0, string.Empty);
        Assert.AreEqual(NatsRun.PlainConnect + "PUB orders.new 4\r\na\r\nb\r\nPING\r\n", NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-ERR-2")]
    public async Task Publish_ErrAfterPub_FailsWithTheServersText()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("-ERR 'Permissions Violation for Publish to greet'\r\n")],
            "-d",
            "hi",
            "nats://localhost/greet");

        NatsRun.AssertExit(result, 8, "curl: (8) NATS server error: Permissions Violation for Publish to greet\n");
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-REQ-1")]
    [SpecRequirement("Nats:NATS-UNSUB-1")]
    public async Task Request_ReplyOnItsInbox_IsWrittenWithoutANewline()
    {
        ScriptStep[] steps = [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\nMSG _INBOX.any 1 4\r\npong\r\n")];

        CurlRunResult first = await NatsRun.RunAsync(steps, "-d", "ping", "nats://localhost/svc.echo?request");
        CurlRunResult second = await NatsRun.RunAsync(steps, "-d", "ping", "nats://localhost/svc.echo?request");

        NatsRun.AssertExit(first, 0, string.Empty, "pong");
        string inbox = InboxOf(NatsRun.ClientText(first));
        Assert.AreEqual(
            NatsRun.PlainConnect + $"SUB {inbox} 1\r\nUNSUB 1 1\r\nPUB svc.echo {inbox} 4\r\nping\r\nPING\r\n",
            NatsRun.ClientText(first));
        Assert.AreNotEqual(inbox, InboxOf(NatsRun.ClientText(second)));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-SUBJ-2")]
    public async Task Publish_SubjectWithEmptyToken_IsRefusedBeforeConnecting()
    {
        CurlRunResult result = await NatsRun.RunAsync([ScriptStep.Send("INFO {}\r\n")], "-d", "hi", "nats://localhost/a..b");

        NatsRun.AssertExit(result, 3, "curl: (3) NATS subject \"a..b\" holds whitespace or an empty token\n");
        Assert.AreEqual(string.Empty, NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-SUBJ-1")]
    public Task Publish_SubjectWithWildcard_IsRefusedBeforeConnecting() =>
        Known.RunCaseAsync(["Nats:NATS-SUBJ-1"], async () =>
        {
            CurlRunResult result = await NatsRun.RunAsync(
                [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\n")],
                "-d",
                "hi",
                "nats://localhost/orders.*");

            Assert.AreEqual(3, result.ExitCode);
            Assert.IsFalse(NatsRun.ClientText(result).Contains("PUB ", StringComparison.Ordinal));
        });

    private static string InboxOf(string clientText) => InboxPattern().Match(clientText).Groups[1].Value;

    [GeneratedRegex(@"SUB (_INBOX\.[0-9a-f]+) 1\r\n")]
    private static partial Regex InboxPattern();
}
