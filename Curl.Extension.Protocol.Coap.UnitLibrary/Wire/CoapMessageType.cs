namespace Curl.Extension.Protocol.Coap.Wire;

/// <summary>The 2-bit Type field of a CoAP message header (RFC 7252 section 3).</summary>
internal enum CoapMessageType
{
    /// <summary>Confirmable (0): the recipient acknowledges it.</summary>
    Confirmable = 0,

    /// <summary>Non-confirmable (1): sent without acknowledgement.</summary>
    NonConfirmable = 1,

    /// <summary>Acknowledgement (2): acknowledges a Confirmable message.</summary>
    Acknowledgement = 2,

    /// <summary>Reset (3): a message the recipient could not process.</summary>
    Reset = 3,
}
