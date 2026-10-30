namespace Curl.Protobuf.Descriptors;

/// <summary>A field's type: one of the scalar types, a named message or enum, or a map.</summary>
public enum ProtoFieldType
{
    /// <summary><c>double</c>.</summary>
    Double,

    /// <summary><c>float</c>.</summary>
    Float,

    /// <summary><c>int32</c>.</summary>
    Int32,

    /// <summary><c>int64</c>.</summary>
    Int64,

    /// <summary><c>uint32</c>.</summary>
    UInt32,

    /// <summary><c>uint64</c>.</summary>
    UInt64,

    /// <summary><c>sint32</c>.</summary>
    SInt32,

    /// <summary><c>sint64</c>.</summary>
    SInt64,

    /// <summary><c>fixed32</c>.</summary>
    Fixed32,

    /// <summary><c>fixed64</c>.</summary>
    Fixed64,

    /// <summary><c>sfixed32</c>.</summary>
    SFixed32,

    /// <summary><c>sfixed64</c>.</summary>
    SFixed64,

    /// <summary><c>bool</c>.</summary>
    Bool,

    /// <summary><c>string</c>.</summary>
    String,

    /// <summary><c>bytes</c>.</summary>
    Bytes,

    /// <summary>A message or enum named by the reference, not yet resolved.</summary>
    Named,

    /// <summary>A <c>map&lt;key, value&gt;</c>.</summary>
    Map,
}
