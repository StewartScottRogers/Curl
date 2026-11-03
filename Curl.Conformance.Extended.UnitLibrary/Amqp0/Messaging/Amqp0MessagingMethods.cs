namespace Curl.Conformance.Extended.Amqp0.Messaging;

/// <summary>
/// Broker answers for the channel, queue and basic classes (BL-4465), added to an <see cref="Amqp0MethodTable"/>
/// through its <see cref="Amqp0MethodTable.With"/> extension point: basic.get answered with get-ok and a message,
/// basic.publish answered with basic.return, queue.declare answered with channel.close, and channel.flow sent before
/// declare-ok.
/// </summary>
public static class Amqp0MessagingMethods
{
    private const ushort ChannelClassId = 20;

    private const ushort QueueClassId = 50;

    private const ushort BasicClassId = Amqp0BrokerContent.BasicClassId;

    /// <summary>
    /// Answers basic.get with get-ok (delivery tag 1, exchange "", routing key "q", no message left) and
    /// <paramref name="body"/> in body frames of <paramref name="bodyFrameSize"/> bytes on <paramref name="bodyChannel"/>,
    /// or on the get's channel when none is given, with a content-type property.
    /// </summary>
    public static Amqp0MethodTable WithGetOk(this Amqp0MethodTable table, byte[] body, int bodyFrameSize, ushort? bodyChannel = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.With(BasicClassId, 70, async (broker, method, token) =>
        {
            await broker.ReplyAsync(method.Channel, BasicClassId, 71, new Amqp0ArgumentWriter().Long(0).Long(1).Octet(0).ShortString(string.Empty).ShortString("q").Long(0), token).ConfigureAwait(false);
            return await broker.SendAsync(Amqp0BrokerContent.Encode(method.Channel, body, bodyFrameSize, bodyChannel ?? method.Channel, "text/plain"), token).ConfigureAwait(false);
        });
    }

    /// <summary>Answers basic.publish with basic.return (<paramref name="replyCode"/>, <paramref name="replyText"/>) and the returned message.</summary>
    public static Amqp0MethodTable WithReturn(this Amqp0MethodTable table, ushort replyCode, string replyText)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.With(BasicClassId, 40, async (broker, method, token) =>
        {
            await broker.ReplyAsync(method.Channel, BasicClassId, 50, new Amqp0ArgumentWriter().Short(replyCode).ShortString(replyText).ShortString(string.Empty).ShortString("q"), token).ConfigureAwait(false);
            return await broker.SendAsync(Amqp0BrokerContent.Encode(method.Channel, "hi"u8.ToArray(), 4088, method.Channel), token).ConfigureAwait(false);
        });
    }

    /// <summary>Answers queue.declare with channel.close (<paramref name="replyCode"/>, <paramref name="replyText"/>, naming queue.declare) and takes the client's close-ok.</summary>
    public static Amqp0MethodTable WithChannelClosedOnDeclare(this Amqp0MethodTable table, ushort replyCode, string replyText)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table
            .With(QueueClassId, 10, (broker, method, token) => broker.ReplyAsync(method.Channel, ChannelClassId, 40, new Amqp0ArgumentWriter().Short(replyCode).ShortString(replyText).Short(QueueClassId).Short(10), token))
            .With(ChannelClassId, 41, (_, _, _) => ValueTask.FromResult(true));
    }

    /// <summary>Sends channel.flow (its active bit <paramref name="active"/>, 1 or 0) before answering queue.declare with declare-ok, and takes the client's flow-ok.</summary>
    public static Amqp0MethodTable WithFlowBeforeDeclareOk(this Amqp0MethodTable table, byte active)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table
            .With(QueueClassId, 10, async (broker, method, token) =>
            {
                await broker.ReplyAsync(method.Channel, ChannelClassId, 20, new Amqp0ArgumentWriter().Octet(active), token).ConfigureAwait(false);
                return await broker.ReplyAsync(method.Channel, QueueClassId, 11, new Amqp0ArgumentWriter().ShortString("q").Long(0).Long(0), token).ConfigureAwait(false);
            })
            .With(ChannelClassId, 21, (_, _, _) => ValueTask.FromResult(true));
    }
}
