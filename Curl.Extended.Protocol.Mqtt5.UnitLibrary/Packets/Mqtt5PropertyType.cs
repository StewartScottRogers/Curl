namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>The data type of a property's value (MQTT 5 section 1.5, named per identifier in table 2-4).</summary>
internal enum Mqtt5PropertyType
{
    /// <summary>One byte.</summary>
    Byte,

    /// <summary>Two Byte Integer.</summary>
    TwoByteInteger,

    /// <summary>Four Byte Integer.</summary>
    FourByteInteger,

    /// <summary>Variable Byte Integer.</summary>
    VariableByteInteger,

    /// <summary>UTF-8 Encoded String.</summary>
    Utf8String,

    /// <summary>Binary Data.</summary>
    BinaryData,

    /// <summary>UTF-8 String Pair.</summary>
    Utf8StringPair,
}
