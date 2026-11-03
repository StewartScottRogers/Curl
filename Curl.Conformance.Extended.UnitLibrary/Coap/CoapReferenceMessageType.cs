namespace Curl.Conformance.Extended.Coap;

/// <summary>The message type field of a CoAP header (RFC 7252 section 3), written by the reference server from the specification.</summary>
public enum CoapReferenceMessageType
{
    /// <summary>Confirmable (0).</summary>
    Confirmable = 0,

    /// <summary>Non-confirmable (1).</summary>
    NonConfirmable = 1,

    /// <summary>Acknowledgement (2).</summary>
    Acknowledgement = 2,

    /// <summary>Reset (3).</summary>
    Reset = 3,
}
