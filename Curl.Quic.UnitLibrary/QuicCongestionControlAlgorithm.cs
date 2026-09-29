namespace Curl.Quic;

/// <summary>
/// The congestion controllers the QUIC client can run over RFC 9002's loss detection
/// (ADR-0144 section 5). curl's ngtcp2 build keeps ngtcp2's default, CUBIC, and has no
/// option to change it.
/// </summary>
public enum QuicCongestionControlAlgorithm
{
    /// <summary>CUBIC (RFC 9438), curl's build's controller and the default.</summary>
    Cubic,

    /// <summary>NewReno, the controller RFC 9002 section 7 and Appendix B specify.</summary>
    NewReno,
}
