namespace Curl.Tls;

/// <summary>Which way a traffic secret protects bytes, seen from the client.</summary>
public enum TlsTrafficDirection
{
    /// <summary>Bytes the client receives from the server.</summary>
    Read,

    /// <summary>Bytes the client sends to the server.</summary>
    Write,
}
