namespace Curl.Tls;

/// <summary>
/// What one ClientHello offering TLS 1.3 and TLS 1.2 (and below) carries
/// (<see cref="TlsClientConnection" />): the TLS 1.3 half builds the hello and runs a TLS
/// 1.3 handshake, and the TLS 1.2 half adds its versions, suites, signature schemes and
/// extensions to it and runs a TLS 1.2-or-below handshake.
/// </summary>
/// <param name="Tls13">What the hello offers for TLS 1.3, and its extension order.</param>
/// <param name="Tls12">
/// What it offers for TLS 1.2 and below: a ceiling of TLS 1.2, since the hello's
/// <c>legacy_version</c> is TLS 1.2, and no session to resume, since the hello's session ID
/// is TLS 1.3's.
/// </param>
public sealed record TlsClientSettings(Tls13ClientSettings Tls13, Tls12ClientSettings Tls12)
{
    /// <summary>Throws when the settings cannot drive one hello.</summary>
    /// <exception cref="ArgumentNullException">A half is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The TLS 1.2 half has a lower ceiling or a session to resume.</exception>
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Tls13);
        ArgumentNullException.ThrowIfNull(Tls12);
        if (Tls12.MaximumVersion != TlsProtocolVersion.Tls12 || Tls12.SessionToResume is not null)
        {
            throw new ArgumentException("Offering TLS 1.2 beside TLS 1.3 needs a TLS 1.2 ceiling and no session to resume.", nameof(Tls12));
        }

        Tls12.Validate();
    }
}
