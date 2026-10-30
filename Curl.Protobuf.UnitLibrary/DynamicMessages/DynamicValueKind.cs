namespace Curl.Protobuf.DynamicMessages;

/// <summary>
/// What a field of a <see cref="DynamicMessage" /> holds, and so which .NET type its values are:
/// the scalar types in the order of <see cref="Descriptors.ProtoFieldType" />, then an enum or a message.
/// </summary>
public enum DynamicValueKind
{
    /// <summary><c>double</c>, held as <see cref="double" />.</summary>
    Double,

    /// <summary><c>float</c>, held as <see cref="float" />.</summary>
    Float,

    /// <summary><c>int32</c>, held as <see cref="int" />.</summary>
    Int32,

    /// <summary><c>int64</c>, held as <see cref="long" />.</summary>
    Int64,

    /// <summary><c>uint32</c>, held as <see cref="uint" />.</summary>
    UInt32,

    /// <summary><c>uint64</c>, held as <see cref="ulong" />.</summary>
    UInt64,

    /// <summary><c>sint32</c>, held as <see cref="int" />.</summary>
    SInt32,

    /// <summary><c>sint64</c>, held as <see cref="long" />.</summary>
    SInt64,

    /// <summary><c>fixed32</c>, held as <see cref="uint" />.</summary>
    Fixed32,

    /// <summary><c>fixed64</c>, held as <see cref="ulong" />.</summary>
    Fixed64,

    /// <summary><c>sfixed32</c>, held as <see cref="int" />.</summary>
    SFixed32,

    /// <summary><c>sfixed64</c>, held as <see cref="long" />.</summary>
    SFixed64,

    /// <summary><c>bool</c>, held as <see cref="bool" />.</summary>
    Bool,

    /// <summary><c>string</c>, held as <see cref="string" />.</summary>
    String,

    /// <summary><c>bytes</c>, held as a <see cref="byte" /> array.</summary>
    Bytes,

    /// <summary>An enum, held as its number in an <see cref="int" />; numbers the enum does not name are kept.</summary>
    Enum,

    /// <summary>A message, held as a <see cref="DynamicMessage" />; a map field's entries are messages too.</summary>
    Message,
}
