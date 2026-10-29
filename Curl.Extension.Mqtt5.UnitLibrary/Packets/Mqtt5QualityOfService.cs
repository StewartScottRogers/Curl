namespace Curl.Extension.Mqtt5.Packets;

/// <summary>The MQTT 5 delivery guarantees of section 4.3; the two-bit value 3 is never valid.</summary>
internal enum Mqtt5QualityOfService
{
    /// <summary>QoS 0.</summary>
    AtMostOnce = 0,

    /// <summary>QoS 1.</summary>
    AtLeastOnce = 1,

    /// <summary>QoS 2.</summary>
    ExactlyOnce = 2,
}
