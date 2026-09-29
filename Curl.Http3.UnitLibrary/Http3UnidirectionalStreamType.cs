namespace Curl.Http3;

/// <summary>
/// The unidirectional stream types of RFC 9114 section 6.2 and RFC 9204 section 4.2: the
/// integer each unidirectional stream starts with.
/// </summary>
public enum Http3UnidirectionalStreamType : long
{
    /// <summary>The control stream (<c>0x00</c>).</summary>
    Control = 0x00,

    /// <summary>A push stream (<c>0x01</c>).</summary>
    Push = 0x01,

    /// <summary>The QPACK encoder stream (<c>0x02</c>).</summary>
    QpackEncoder = 0x02,

    /// <summary>The QPACK decoder stream (<c>0x03</c>).</summary>
    QpackDecoder = 0x03,
}
