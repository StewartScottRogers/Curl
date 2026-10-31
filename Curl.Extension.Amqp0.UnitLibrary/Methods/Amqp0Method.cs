using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.Methods;

/// <summary>
/// One AMQP 0-9-1 method (section 4.2.4): a method frame's payload is the class id, the method id, then the
/// arguments. Each method writes its own arguments; <see cref="Amqp0MethodReader"/> reads them.
/// </summary>
internal abstract record Amqp0Method
{
    /// <summary>The class id (amqp0-9-1.extended.xml): connection 10, channel 20, queue 50, basic 60.</summary>
    public abstract ushort ClassId { get; }

    /// <summary>The method id within its class.</summary>
    public abstract ushort MethodId { get; }

    /// <summary>The method frame payload: class id, method id and arguments.</summary>
    public byte[] ToPayload()
    {
        var writer = new Amqp0Writer();
        writer.WriteShort(ClassId);
        writer.WriteShort(MethodId);
        WriteArguments(writer);
        return writer.ToArray();
    }

    /// <summary>The method frame carrying this method on <paramref name="channel"/>.</summary>
    public Amqp0Frame ToFrame(ushort channel) => new(Amqp0FrameType.Method, channel, ToPayload());

    /// <summary>Writes the method's arguments in the order its class reference lists them.</summary>
    protected abstract void WriteArguments(Amqp0Writer writer);
}
