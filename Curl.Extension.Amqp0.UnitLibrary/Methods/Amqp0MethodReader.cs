using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.Methods;

/// <summary>
/// Reads a method frame's payload into its <see cref="Amqp0Method"/>. A class and method id pair this library does not
/// know is a 503 command-invalid; a truncated argument or bytes after the last one is a 502 syntax-error.
/// </summary>
internal static class Amqp0MethodReader
{
    private static readonly Dictionary<(ushort ClassId, ushort MethodId), Func<Amqp0Reader, Amqp0Method>> Readers = new()
    {
        [(10, 10)] = Amqp0ConnectionStart.ReadArguments,
        [(10, 11)] = Amqp0ConnectionStartOk.ReadArguments,
        [(10, 30)] = Amqp0ConnectionTune.ReadArguments,
        [(10, 31)] = Amqp0ConnectionTuneOk.ReadArguments,
        [(10, 40)] = Amqp0ConnectionOpen.ReadArguments,
        [(10, 41)] = Amqp0ConnectionOpenOk.ReadArguments,
        [(10, 50)] = Amqp0ConnectionClose.ReadArguments,
        [(10, 51)] = _ => new Amqp0ConnectionCloseOk(),
        [(20, 10)] = Amqp0ChannelOpen.ReadArguments,
        [(20, 11)] = Amqp0ChannelOpenOk.ReadArguments,
        [(20, 40)] = Amqp0ChannelClose.ReadArguments,
        [(20, 41)] = _ => new Amqp0ChannelCloseOk(),
        [(50, 10)] = Amqp0QueueDeclare.ReadArguments,
        [(50, 11)] = Amqp0QueueDeclareOk.ReadArguments,
        [(60, 40)] = Amqp0BasicPublish.ReadArguments,
        [(60, 70)] = Amqp0BasicGet.ReadArguments,
        [(60, 71)] = Amqp0BasicGetOk.ReadArguments,
        [(60, 72)] = Amqp0BasicGetEmpty.ReadArguments,
        [(60, 80)] = Amqp0BasicAck.ReadArguments,
    };

    /// <summary>Reads the method in <paramref name="payload"/>, a method frame's payload.</summary>
    public static Amqp0Method Read(byte[] payload)
    {
        var reader = new Amqp0Reader(payload);
        ushort classId = reader.ReadShort();
        ushort methodId = reader.ReadShort();
        if (!Readers.TryGetValue((classId, methodId), out Func<Amqp0Reader, Amqp0Method>? read))
        {
            throw Amqp0WireFormatException.CommandInvalid($"Method {classId}.{methodId} is not one this client knows.");
        }

        Amqp0Method method = read(reader);
        reader.EnsureAtEnd($"Method {classId}.{methodId}");
        return method;
    }

    /// <summary>Reads the method a method frame carries; any other frame type is a 505 unexpected-frame.</summary>
    public static Amqp0Method Read(Amqp0Frame frame)
    {
        if (frame.Type != Amqp0FrameType.Method)
        {
            throw Amqp0WireFormatException.UnexpectedFrame($"A {frame.Type} frame came where a method frame was expected.");
        }

        return Read(frame.Payload);
    }
}
