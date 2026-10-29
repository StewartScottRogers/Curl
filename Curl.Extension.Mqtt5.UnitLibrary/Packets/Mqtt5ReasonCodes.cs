using Curl.Extension.Mqtt5.Encoding;

namespace Curl.Extension.Mqtt5.Packets;

/// <summary>The success/failure split of MQTT 5 reason codes (section 2.4) and reading one from a packet.</summary>
internal static class Mqtt5ReasonCodes
{
    /// <summary>Whether <paramref name="reasonCode"/> reports a failure: 0x80 and above.</summary>
    public static bool IsFailure(Mqtt5ReasonCode reasonCode) => (byte)reasonCode >= 0x80;

    /// <summary>Whether <paramref name="reasonCode"/> reports success: below 0x80.</summary>
    public static bool IsSuccess(Mqtt5ReasonCode reasonCode) => !IsFailure(reasonCode);

    /// <summary>Reads one reason code byte; a value table 2-6 does not name is a Malformed Packet.</summary>
    public static Mqtt5ReasonCode Read(Mqtt5Reader reader)
    {
        var reasonCode = (Mqtt5ReasonCode)reader.ReadByte();
        return Enum.IsDefined(reasonCode)
            ? reasonCode
            : throw Mqtt5MalformedPacketException.Malformed($"0x{(byte)reasonCode:X2} is not an MQTT 5 reason code.");
    }
}
