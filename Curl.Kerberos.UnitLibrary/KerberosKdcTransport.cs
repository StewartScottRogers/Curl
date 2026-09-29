namespace Curl.Kerberos;

/// <summary>How a KDC is reached.</summary>
public enum KerberosKdcTransport
{
    /// <summary>
    /// UDP or TCP, chosen per message by <see cref="KerberosConfiguration.UdpPreferenceLimit" />:
    /// a <c>kdc</c> entry with no <c>udp/</c>, <c>tcp/</c> or <c>https://</c> prefix.
    /// </summary>
    UdpOrTcp,

    /// <summary>UDP only: a <c>udp/</c> entry or a <c>_kerberos._udp</c> SRV record.</summary>
    Udp,

    /// <summary>TCP only: a <c>tcp/</c> entry or a <c>_kerberos._tcp</c> SRV record.</summary>
    Tcp,

    /// <summary>HTTPS through an MS-KKDCP proxy: an <c>https://</c> entry.</summary>
    Https,
}
