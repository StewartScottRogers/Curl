using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.BlockwiseObserve;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.BlockwiseObserve;

/// <summary>RFC 7641 sections 3 and 4 and RFC 7959 section 3.4: <c>--coap-observe</c> registers, writes notifications in order, acknowledges confirmable ones, re-registers and deregisters.</summary>
[TestClass]
public sealed class CoapObserveTests
{
    private const string Observe = "--coap-observe";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7641-3.1-1")]
    [SpecRequirement("Coap:RFC7641-3.2-1")]
    [SpecRequirement("Coap:RFC7641-3.4-1")]
    [SpecRequirement("Coap:RFC7641-3.2-2")]
    public Task Registration_NotificationsAreWrittenInOrderAndAReorderedOneIsDropped() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7641-3.1-1", "Coap:RFC7641-3.2-1", "Coap:RFC7641-3.4-1", "Coap:RFC7641-3.2-2"], async () =>
        {
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((registration, _) =>
            [
                CoapResponses.Piggybacked(registration, CoapResponses.Content, "two"u8.ToArray(), ObserveOption(2)),
                Notification(registration, 0x1000, "five", ObserveOption(5)),
                Notification(registration, 0x1001, "three", ObserveOption(3)),
                Notification(registration, 0x1002, "seven", ObserveOption(7)),
                Notification(registration, 0x1003, "end"),
            ]);

            CurlRunResult result = await CoapBlockwiseObserveRun.RunAsync(server, TestContext.CancellationToken, Observe, CoapBlockwiseObserveRun.Url);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("two\nfive\nseven\nend\n", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            CoapReferenceMessage sent = server.Received.Single().Message;
            Assert.AreEqual(CoapReferenceMessageType.Confirmable, sent.Type);
            Assert.AreEqual((byte)0x01, sent.Code);
            Assert.AreEqual(0u, CoapBlockwiseObserveOptions.UintOf(sent, CoapBlockwiseObserveOptions.Observe));
            Assert.IsNotEmpty(sent.Token);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7641-3.5-1")]
    [SpecRequirement("Coap:RFC7641-3.2-3")]
    public Task ConfirmableNotifications_EachIsAcknowledgedWithAnEmptyAcknowledgement() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7641-3.5-1", "Coap:RFC7641-3.2-3"], async () =>
        {
            CoapReferenceMessage? registered = null;
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((message, index) =>
            {
                registered ??= message;
                return index switch
                {
                    0 => [CoapResponses.Piggybacked(message, CoapResponses.Content, "a"u8.ToArray(), ObserveOption(1)), Notification(registered, 0x2000, "b", CoapReferenceMessageType.Confirmable, ObserveOption(2))],
                    1 => [Notification(registered, 0x2001, "c", CoapReferenceMessageType.Confirmable, ObserveOption(3))],
                    _ => [CoapNotifications.Notification(registered, CoapReferenceMessageType.NonConfirmable, 0x2002, CoapBlockwiseObserveRun.NotFound, "gone"u8.ToArray())],
                };
            });

            CurlRunResult result = await CoapBlockwiseObserveRun.RunAsync(server, TestContext.CancellationToken, Observe, CoapBlockwiseObserveRun.Url);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("a\nb\nc\ngone\n", Encoding.UTF8.GetString(result.StandardOutput));
            IReadOnlyList<CoapReceivedDatagram> sent = server.Received;
            Assert.HasCount(3, sent);
            CollectionAssert.AreEqual(new CoapReferenceMessage(1, CoapReferenceMessageType.Acknowledgement, 0, 0x2000, [], [], []).Encode(), sent[1].Bytes);
            CollectionAssert.AreEqual(new CoapReferenceMessage(1, CoapReferenceMessageType.Acknowledgement, 0, 0x2001, [], [], []).Encode(), sent[2].Bytes);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7641-3.3.1-1")]
    [SpecRequirement("Coap:RFC7641-3.4-2")]
    public Task MaxAgePassesWithoutANotification_ReregistersAndTakesAnOlderSequenceAfter128Seconds() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7641-3.3.1-1", "Coap:RFC7641-3.4-2"], async () =>
        {
            TimerJumpingClock clock = new();
            CoapReferenceMessage? registered = null;
            CoapReferenceServer server = CoapMessagingRun.Server(clock, (message, _) =>
            {
                if (registered is null)
                {
                    registered = message;
                    return [
                        CoapResponses.Piggybacked(message, CoapResponses.Content, "ten"u8.ToArray(), ObserveOption(10), CoapBlockwiseObserveOptions.Uint(CoapBlockwiseObserveOptions.MaxAge, 200)),
                    ];
                }

                return message.Type == CoapReferenceMessageType.NonConfirmable
                    ? [Notification(registered, 0x3001, "late", ObserveOption(4)), Notification(registered, 0x3002, "end")]
                    : [];
            });

            CurlRunResult result = await CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, Observe, CoapBlockwiseObserveRun.Url);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("ten\nlate\nend\n", Encoding.UTF8.GetString(result.StandardOutput));
            CoapReceivedDatagram first = server.Received[0];
            CoapReceivedDatagram reregistration = server.Received.Single(datagram => datagram.Message.Type == CoapReferenceMessageType.NonConfirmable);
            Assert.IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(205), reregistration.ReceivedAt - first.ReceivedAt);
            Assert.AreEqual(0u, CoapBlockwiseObserveOptions.UintOf(reregistration.Message, CoapBlockwiseObserveOptions.Observe));
            CollectionAssert.AreEqual(first.Message.Token, reregistration.Message.Token);
            Assert.AreNotEqual(first.Message.MessageId, reregistration.Message.MessageId);
            Assert.AreEqual(first.Message.Code, reregistration.Message.Code);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7641-3.6-1")]
    public Task TransferEndsByMaxTime_DeregistersWithObserve1AndTheRegistrationToken() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7641-3.6-1"], async () =>
        {
            TimerJumpingClock clock = new();
            CoapReferenceServer server = CoapMessagingRun.Server(clock, (message, index) => index == 0
                ? [CoapResponses.Piggybacked(message, CoapResponses.Content, "one"u8.ToArray(), ObserveOption(1))]
                : []);

            CurlRunResult result = await CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, Observe, "-m", "10", CoapBlockwiseObserveRun.Url);

            Assert.AreEqual(28, result.ExitCode);
            StringAssert.StartsWith(Encoding.UTF8.GetString(result.StandardError), "curl: (28) ");
            Assert.AreEqual("one\n", Encoding.UTF8.GetString(result.StandardOutput));
            CoapReferenceMessage registration = server.Received[0].Message;
            CoapReferenceMessage deregistration = server.Received[^1].Message;
            Assert.AreEqual(registration.Code, deregistration.Code);
            Assert.AreEqual(1u, CoapBlockwiseObserveOptions.UintOf(deregistration, CoapBlockwiseObserveOptions.Observe));
            CollectionAssert.AreEqual(registration.Token, deregistration.Token);
            Assert.AreNotEqual(registration.MessageId, deregistration.MessageId);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7959-3.4-1")]
    public Task NotificationWithBlock2_LaterBlocksAreFetchedWithoutObserveUnderAnotherTokenAndWrittenWhole() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7959-3.4-1"], async () =>
        {
            CoapReferenceMessage? registered = null;
            CoapReferenceServer server = CoapBlockwiseObserveRun.Server((message, index) =>
            {
                registered ??= message;
                return index == 0
                    ? [CoapResponses.Piggybacked(message, CoapResponses.Content, "0123456789abcdef"u8.ToArray(), ObserveOption(1), Block2(0, true))]
                    : [CoapResponses.Piggybacked(message, CoapResponses.Content, "tail"u8.ToArray(), Block2(1, false)), Notification(registered, 0x4000, "end")];
            });

            CurlRunResult result = await CoapBlockwiseObserveRun.RunAsync(server, TestContext.CancellationToken, Observe, CoapBlockwiseObserveRun.Url);

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("0123456789abcdeftail\nend\n", Encoding.UTF8.GetString(result.StandardOutput));
            IReadOnlyList<CoapReceivedDatagram> sent = server.Received;
            Assert.HasCount(2, sent);
            CoapReferenceMessage fetch = sent[1].Message;
            Assert.IsNull(CoapBlockwiseObserveOptions.UintOf(fetch, CoapBlockwiseObserveOptions.Observe));
            Assert.AreEqual(new CoapReferenceBlock(1, false, 0), CoapBlockwiseObserveOptions.BlockOf(fetch, CoapBlockwiseObserveOptions.Block2));
            CollectionAssert.AreNotEqual(sent[0].Message.Token, fetch.Token);
        });

    private static CoapReferenceOption ObserveOption(uint sequence) => CoapBlockwiseObserveOptions.Uint(CoapBlockwiseObserveOptions.Observe, sequence);

    private static CoapReferenceOption Block2(uint number, bool more) =>
        CoapBlockwiseObserveOptions.Block(CoapBlockwiseObserveOptions.Block2, new CoapReferenceBlock(number, more, 0));

    private static byte[] Notification(CoapReferenceMessage registration, ushort messageId, string payload, params IReadOnlyList<CoapReferenceOption> options) =>
        Notification(registration, messageId, payload, CoapReferenceMessageType.NonConfirmable, options);

    private static byte[] Notification(CoapReferenceMessage registration, ushort messageId, string payload, CoapReferenceMessageType type, params IReadOnlyList<CoapReferenceOption> options) =>
        CoapNotifications.Notification(registration, type, messageId, CoapResponses.Content, Encoding.ASCII.GetBytes(payload), options);
}
