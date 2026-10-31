namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>The Retain Handling subscription option of MQTT 5 section 3.8.3.1; the value 3 is a Protocol Error.</summary>
internal enum Mqtt5RetainHandling
{
    /// <summary>0: send retained messages at the time of the subscribe.</summary>
    SendAtSubscribe = 0,

    /// <summary>1: send retained messages only when the subscription did not already exist.</summary>
    SendAtNewSubscribe = 1,

    /// <summary>2: never send retained messages at the time of the subscribe.</summary>
    DoNotSend = 2,
}
