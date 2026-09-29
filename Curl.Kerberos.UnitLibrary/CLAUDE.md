# Curl.Kerberos.UnitLibrary

Hand-built Kerberos for every platform: a Kerberos V5 client (RFC 4120) and the GSS-API
Kerberos mechanism (RFC 4121) wrapped around it. It is shared by HTTP and proxy
Negotiate (`Curl.Authentication.UnitLibrary`), SASL `GSSAPI` (BL-538), SOCKS5 GSS-API
(BL-615) and FTP `--krb` (BL-693). Where the platform's own Kerberos answers instead
(SSPI on Windows, the system GSS-API elsewhere) is ADR-0142's routing, not this
library's.

Namespace `Curl.Kerberos`. It reads MIT's credential cache (version 4) and keytab
(version 2) files and finds the default cache and keytab as MIT does
(`CredentialCacheReader`, `KeytabReader`, `CredentialCacheStore`, `KeytabStore`;
BL-688, ADR-0158). It reads `krb5.conf` as MIT's profile library does, maps a host to
its realm and locates a realm's KDCs from the file or from DNS SRV records through
`IKerberosSrvLookup` (`KerberosConfigurationStore`, `KerberosConfigurationReader`,
`KerberosConfiguration`, `KerberosKdcLocator`; BL-689, ADR-0160). It encrypts,
decrypts, checksums and makes keys from passwords with `aes128`/`aes256-cts-hmac-sha1-96`
(RFC 3962), `aes128-cts-hmac-sha256-128`, `aes256-cts-hmac-sha384-192` (RFC 8009) and
`rc4-hmac` (RFC 4757): `KerberosEncryption.Create` gives one per `KerberosEncryptionType`,
confounders come from `IKerberosRandomSource`, and a failed integrity check throws
`KerberosCryptographyException` (`KerberosNFold`, `KerberosAesCts`,
`AesSha1KerberosEncryption`, `AesSha2KerberosEncryption`, `Rc4HmacKerberosEncryption`;
BL-686, ADR-0161). The client and the GSS-API mechanism land under their own tasks.

## Rules

- **Base class library plus `Curl.Cryptography.UnitLibrary` only.** No package, and no
  other project reference. `Curl.Cryptography.UnitLibrary` supplies AES-CBC-CTS, MD4
  and RC4 (BL-686).
- **Never a `Socket`.** The KDC is reached through an injected transport, so every
  exchange is testable with recorded bytes and no network.
- **Files through an injected seam.** The credential cache, the keytab and `krb5.conf`
  are read through an interface the caller supplies (`IKerberosFileReader`), never
  `System.IO.File` directly.
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
