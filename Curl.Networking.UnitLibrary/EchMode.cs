namespace Curl.Networking;

/// <summary>
/// What curl 8.21.0's <c>--ech</c> asks of a handshake (ADR-0326): its <c>CURLOPT_ECH</c> bits as
/// <c>lib/setopt.c</c> sets them from the mode and the <c>ecl:</c> list the tool passes after it.
/// </summary>
public enum EchMode
{
    /// <summary>No Encrypted Client Hello: <c>--ech false</c>, no <c>--ech</c>, or only <c>pn:</c>.</summary>
    Off,

    /// <summary><c>--ech grease</c>: a GREASE <c>encrypted_client_hello</c>, whatever list is given.</summary>
    Grease,

    /// <summary><c>--ech true</c>, or an <c>ecl:</c> list alone: ECH when a usable configuration is found, a plain hello otherwise.</summary>
    Opportunistic,

    /// <summary><c>--ech hard</c>: ECH with a usable configuration, or the transfer fails with exit 35.</summary>
    Mandatory,
}
