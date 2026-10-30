namespace Curl.HttpDatagram;

/// <summary>
/// The capsule types RFC 9297 registers.
/// </summary>
public static class CapsuleType
{
    /// <summary>
    /// The <c>DATAGRAM</c> capsule (0x00), which carries one HTTP Datagram's payload.
    /// </summary>
    public const ulong Datagram = 0x00;
}
