namespace Curl.Conformance.Extended.Amqp0;

/// <summary>Answers one client method; returns false when the connection is over.</summary>
public delegate ValueTask<bool> Amqp0MethodHandler(Amqp0ReferenceBroker broker, Amqp0BrokerFrame method, CancellationToken cancellationToken);

/// <summary>
/// The reference broker's answer to each client method, by class and method id. <see cref="Standard"/> holds the
/// connection class (start-ok, tune-ok, open, close, close-ok) and a minimal session (channel.open and close,
/// queue.declare, basic.publish, basic.get answered get-empty); a case or a later part of the plan replaces or adds
/// entries with <see cref="With"/> without editing this file.
/// </summary>
public sealed class Amqp0MethodTable
{
    private readonly Dictionary<(ushort ClassId, ushort MethodId), Amqp0MethodHandler> handlers;

    private Amqp0MethodTable(Dictionary<(ushort ClassId, ushort MethodId), Amqp0MethodHandler> handlers) => this.handlers = handlers;

    /// <summary>The connection class and the minimal session.</summary>
    public static Amqp0MethodTable Standard { get; } = new Amqp0MethodTable([])
        .With(10, 11, (broker, _, token) => broker.ReplyAsync(0, 10, 30, new Amqp0ArgumentWriter().Short(broker.Settings.ChannelMax).Long(broker.Settings.FrameMax).Short(broker.Settings.Heartbeat), token))
        .With(10, 31, (_, _, _) => ValueTask.FromResult(true))
        .With(10, 40, (broker, _, token) => broker.ReplyAsync(0, 10, 41, new Amqp0ArgumentWriter().ShortString(string.Empty), token))
        .With(10, 50, async (broker, _, token) => !await broker.ReplyAsync(0, 10, 51, new Amqp0ArgumentWriter(), token).ConfigureAwait(false))
        .With(10, 51, (_, _, _) => ValueTask.FromResult(false))
        .With(20, 10, (broker, method, token) => broker.ReplyAsync(method.Channel, 20, 11, new Amqp0ArgumentWriter().LongString([]), token))
        .With(20, 40, (broker, method, token) => broker.ReplyAsync(method.Channel, 20, 41, new Amqp0ArgumentWriter(), token))
        .With(50, 10, (broker, method, token) => broker.ReplyAsync(method.Channel, 50, 11, new Amqp0ArgumentWriter().ShortString(QueueOf(method)).Long(0).Long(0), token))
        .With(60, 40, (_, _, _) => ValueTask.FromResult(true))
        .With(60, 70, (broker, method, token) => broker.ReplyAsync(method.Channel, 60, 72, new Amqp0ArgumentWriter().ShortString(string.Empty), token));

    /// <summary>A table with <paramref name="handler"/> answering class <paramref name="classId"/> method <paramref name="methodId"/>.</summary>
    public Amqp0MethodTable With(ushort classId, ushort methodId, Amqp0MethodHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new Amqp0MethodTable(new(handlers) { [(classId, methodId)] = handler });
    }

    /// <summary>The handler of a method; a method no entry answers throws, failing the run.</summary>
    internal Amqp0MethodHandler HandlerOf(Amqp0BrokerFrame method) => handlers[(method.ClassId, method.MethodId)];

    // queue.declare: reserved-1 short, then the queue name.
    private static string QueueOf(Amqp0BrokerFrame method)
    {
        Amqp0ArgumentReader arguments = method.Arguments();
        arguments.Short();
        return arguments.ShortString();
    }
}
