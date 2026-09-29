# Curl.Kerberos.UnitLibrary

Hand-built Kerberos for every platform: a Kerberos V5 client (RFC 4120) and the GSS-API
Kerberos mechanism (RFC 4121) wrapped around it. It is shared by HTTP and proxy
Negotiate (`Curl.Authentication.UnitLibrary`), SASL `GSSAPI` (BL-538), SOCKS5 GSS-API
(BL-615) and FTP `--krb` (BL-693). Where the platform's own Kerberos answers instead
(SSPI on Windows, the system GSS-API elsewhere) is ADR-0142's routing, not this
library's.

Namespace `Curl.Kerberos`. The project is scaffolded (BL-685) and holds no types yet;
the client, its encryption types and the GSS-API mechanism land under their own tasks,
starting with BL-686.

## Rules

- **Base class library plus `Curl.Cryptography.UnitLibrary` only.** No package, and no
  other project reference. The reference to `Curl.Cryptography.UnitLibrary` is added by
  the first task that needs one of its primitives (BL-686), not before.
- **Never a `Socket`.** The KDC is reached through an injected transport, so every
  exchange is testable with recorded bytes and no network.
- **Files through an injected seam.** The credential cache, the keytab and `krb5.conf`
  are read through an interface the caller supplies, never `System.IO.File` directly.
- **Time through `TimeProvider`.** Ticket lifetimes, authenticator timestamps and clock
  skew all take the injected `TimeProvider`; never `DateTime.Now` or `Thread.Sleep`.
- **Randomness injected.** Confounders, session subkeys and nonces come from an injected
  source, so published test vectors and recorded exchanges reproduce.
- **Secrets zeroed.** Keys and decrypted material are cleared with
  `CryptographicOperations.ZeroMemory` once used, as in `Curl.Cryptography.UnitLibrary`.
- Tests in `Curl.Kerberos.UnitTests` are platform-neutral and pin RFC test vectors with
  their source cited beside each.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
