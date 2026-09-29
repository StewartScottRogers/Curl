namespace Curl.Kerberos;

/// <summary>Why a GSS-API Kerberos context could not be established or a per-message token was refused.</summary>
public enum KerberosGssError
{
    /// <summary>The token is not a Kerberos token of the kind expected: bad framing, mechanism, token ID, filler, flags or length.</summary>
    MalformedToken,

    /// <summary>The acceptor answered with a KRB-ERROR; <see cref="KerberosGssException.KerberosErrorCode" /> holds its code.</summary>
    AcceptorError,

    /// <summary>The AP-REP did not decrypt in the session key, or does not echo the authenticator's time: the acceptor is not who it claims.</summary>
    MutualAuthenticationFailed,

    /// <summary>A per-message token's checksum or encryption does not verify: it was altered, or is for another context.</summary>
    IntegrityCheckFailed,

    /// <summary>A per-message token's sequence number or direction is not the next expected from the acceptor: replayed, reordered, lost or reflected.</summary>
    BadSequenceNumber,
}
