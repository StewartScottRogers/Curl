using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Amqp1.Messaging;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Amqp1.Messaging;

/// <summary>The messaging link behaviours on frames curl never sends them: they leave such frames unanswered.</summary>
[TestClass]
public sealed class Amqp1ReferenceLinksTests
{
    private static (MemoryStream Stream, Amqp1ReferenceSession Session) NewSession()
    {
        MemoryStream stream = new();
        return (stream, new Amqp1ReferenceSession(new ReferenceConnection(stream, new WireExchange(5672, ReferenceTransport.Tcp)), new Amqp1ReferenceEncoder(false), CancellationToken.None));
    }

    [TestMethod]
    public async Task SendingLink_EmptyFrame_SendsNothing()
    {
        var (stream, session) = NewSession();
        Amqp1ReferenceSendingLink link = new([new(Amqp1ReferenceMessage.Transfer(0, 0, false, false), [])]);

        await link.ReceivedAsync(session, new Amqp1ReferenceFrame(Amqp1ReferenceFrame.AmqpType, 0, null, []));

        Assert.AreEqual(0, stream.Length);
    }

    [TestMethod]
    public async Task ReceivingLink_EmptyFrame_SendsNothing()
    {
        var (stream, session) = NewSession();
        Amqp1ReferenceReceivingLink link = new(new Amqp1ReferenceDescribed(Amqp1ReferenceMessage.Accepted, Array.Empty<object?>()), TimeSpan.Zero);

        await link.ReceivedAsync(session, new Amqp1ReferenceFrame(Amqp1ReferenceFrame.AmqpType, 0, null, []));

        Assert.AreEqual(0, stream.Length);
        Assert.AreEqual(0, link.TransfersWithoutCredit);
    }

    [TestMethod]
    public async Task ReceivingLink_TransferBeforeCredit_CountsIt()
    {
        var (_, session) = NewSession();
        Amqp1ReferenceReceivingLink link = new(new Amqp1ReferenceDescribed(Amqp1ReferenceMessage.Accepted, Array.Empty<object?>()), TimeSpan.Zero);

        await link.ReceivedAsync(session, new Amqp1ReferenceFrame(Amqp1ReferenceFrame.AmqpType, 0, Amqp1ReferenceMessage.Transfer(0, 0, true, false), []));

        Assert.AreEqual(1, link.TransfersWithoutCredit);
    }
}
