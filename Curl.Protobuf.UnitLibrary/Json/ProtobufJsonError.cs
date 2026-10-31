namespace Curl.Protobuf.Json;

/// <summary>Why JSON could not be converted to or from a message; the conversions never throw for bad input.</summary>
public enum ProtobufJsonError
{
    /// <summary>The conversion succeeded.</summary>
    None,

    /// <summary>The input is not well-formed JSON.</summary>
    MalformedJson,

    /// <summary>A JSON member names no field of the message, by its JSON name or its original name.</summary>
    UnknownField,

    /// <summary>A JSON value is of a kind the field cannot hold, such as a string for a message or an array for a singular field.</summary>
    WrongType,

    /// <summary>A JSON value of the right kind is not a value of the field: out of range, not integral, not base64 or no enum value's name.</summary>
    InvalidValue,

    /// <summary>Messages nest deeper than <see cref="Wire.ProtobufReader.MaximumDepth" />.</summary>
    NestingTooDeep,

    /// <summary>The binary input is not a well-formed message of the type.</summary>
    MalformedBinary,
}
