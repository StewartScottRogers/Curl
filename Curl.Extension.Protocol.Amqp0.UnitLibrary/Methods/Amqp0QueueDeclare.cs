using Curl.Extension.Protocol.Amqp0.Wire;

namespace Curl.Extension.Protocol.Amqp0.Methods;

/// <summary>
/// queue.declare (50.10): the reserved ticket short, the queue name, the passive, durable, exclusive, auto-delete and
/// no-wait bits, and the arguments table.
/// </summary>
internal sealed record Amqp0QueueDeclare(string Queue) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 50;

    /// <inheritdoc />
    public override ushort MethodId => 10;

    /// <summary>Only checks that the queue exists, never creates it.</summary>
    public bool Passive { get; init; }

    /// <summary>The queue survives a broker restart.</summary>
    public bool Durable { get; init; }

    /// <summary>Only this connection may use the queue, and it goes when the connection closes.</summary>
    public bool Exclusive { get; init; }

    /// <summary>The queue goes when its last consumer does.</summary>
    public bool AutoDelete { get; init; }

    /// <summary>The server sends no declare-ok.</summary>
    public bool NoWait { get; init; }

    /// <summary>The declaration arguments (x-message-ttl and the like).</summary>
    public Amqp0FieldTable Arguments { get; init; } = Amqp0FieldTable.Empty;

    /// <summary>Reads queue.declare's arguments.</summary>
    internal static Amqp0QueueDeclare ReadArguments(Amqp0Reader reader)
    {
        _ = reader.ReadShort();
        string queue = reader.ReadShortString();
        bool[] bits = reader.ReadBits(5);
        return new(queue)
        {
            Passive = bits[0],
            Durable = bits[1],
            Exclusive = bits[2],
            AutoDelete = bits[3],
            NoWait = bits[4],
            Arguments = reader.ReadFieldTable(),
        };
    }

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShort(0);
        writer.WriteShortString(Queue);
        writer.WriteBits(Passive, Durable, Exclusive, AutoDelete, NoWait);
        writer.WriteFieldTable(Arguments);
    }
}
