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
BL-688, ADR-0158). `CredentialCacheStore` also reads a `DIR:` collection's cache (the
one its `primary` file names, or `DIR::/dir/tktN` directly) and a `KCM:` cache from the
KCM daemon through the injected `IKerberosKcmConnector`, speaking MIT `cc_kcm.c`'s
protocol (`KerberosKcmClient`, `KcmCredentialCacheReader`; BL-789), and takes the
default cache name from `krb5.conf`'s `default_ccache_name` when `KRB5CCNAME` is unset;
`KeytabStore` likewise takes `default_keytab_name` when `KRB5_KTNAME` is unset, and both
expand MIT's Unix `%{token}` parameters (`%{uid}`, `%{euid}`, `%{USERID}`, `%{username}`,
`%{TEMP}`, `%{LIBDIR}`, `%{BINDIR}`, `%{SBINDIR}`, `%{null}`) in the configured value
(`KerberosPathExpansion`; BL-816), an unclosed or unknown token failing as
`KerberosFileError.PathTokenInvalid`. It reads `krb5.conf` as MIT's profile library does, maps a host to
its realm and locates a realm's KDCs from the file or from DNS SRV records through
`IKerberosSrvLookup` (`KerberosConfigurationStore`, `KerberosConfigurationReader`,
`KerberosConfiguration`, `KerberosKdcLocator`; BL-689, ADR-0160). It encrypts,
decrypts, checksums and makes keys from passwords with `aes128`/`aes256-cts-hmac-sha1-96`
(RFC 3962), `aes128-cts-hmac-sha256-128`, `aes256-cts-hmac-sha384-192` (RFC 8009),
`rc4-hmac` (RFC 4757), `camellia128-cts-cmac`, `camellia256-cts-cmac` (RFC 6803) and
`des3-cbc-sha1` (RFC 3961 section 6.3; `Des3CbcSha1KerberosEncryption`, BL-894, ADR-0232,
its zero padding dropped before a decrypted value is decoded, `KerberosAsn1.WithoutPadding`):
`KerberosEncryption.Create` gives one per `KerberosEncryptionType`,
confounders come from `IKerberosRandomSource`, and a failed integrity check throws
`KerberosCryptographyException` (`KerberosNFold`, `KerberosAesCts`,
`AesSha1KerberosEncryption`, `AesSha2KerberosEncryption`, `Rc4HmacKerberosEncryption`;
BL-686, ADR-0161; `CamelliaCmacKerberosEncryption` over `Curl.Cryptography`'s
`Camellia` with `KerberosCamelliaCts` and `KerberosCamelliaCmac`, BL-893). It encodes in DER and decodes (BER accepted) the Kerberos V5
messages with `System.Formats.Asn1`: `KerberosKdcRequest` (AS-REQ, TGS-REQ),
`KerberosKdcReply` (AS-REP, TGS-REP), `KerberosEncryptedKdcReplyPart`,
`KerberosApRequest`, `KerberosAuthenticator`, `KerberosApReply`,
`KerberosEncryptedApReplyPart`, `KerberosErrorMessage`, `KerberosTicket` and the
structures inside them, with `KerberosMessage.PeekType` to tell a reply from a
KRB-ERROR; a malformed or unexpected message throws `KerberosMessageException`
(`KerberosAsn1` holds the shared ASN.1 pieces; BL-687, ADR-0163). It gets tickets from
the KDC: `KerberosKdcClient` returns a `KerberosCredential` for a service from the
credential cache, by a TGS exchange with the cache's ticket-granting ticket, or from a
`KerberosPasswordCredential` by an AS exchange with `PA-ENC-TIMESTAMP` and then a TGS
exchange, over the injected `IKerberosKdcTransport` (`KerberosKdcSender` picks UDP or
TCP and frames TCP) and, for an `https://` KDC, the optional `IKerberosKdcProxyTransport`
with the request wrapped in MS-KKDCP's `KerberosKdcProxyMessage` as MIT does (BL-827;
without one, `https://` KDCs are skipped); every refusal is a `KerberosKdcException` with a `KerberosKdcError`
(BL-690, ADR-0168), and follows the KDCs' cross-realm referrals (`krbtgt/OTHER@REALM`) to
OTHER's KDCs up to `MaximumReferralHops` times (BL-826, ADR-0200); given a
`CredentialCacheStore` and a cache name instead, it stores a ticket got by a TGS exchange
back in a `FILE:` or `DIR:` cache by appending it through the injected
`IKerberosFileWriter`, as MIT's `cc_file.c` does, ignoring a failed store as MIT does
(`CredentialCacheWriter`, `CredentialCacheStore.Store`; BL-825, ADR-0208), or in a
`KCM:` cache by sending the KCM daemon `KCM_OP_STORE` with the cache name and the
marshalled credential, as MIT's `cc_kcm.c` does (`KcmCredentialCacheWriter`; BL-891). Its AS-REQs offer
`default_tkt_enctypes` and its TGS-REQs `default_tgs_enctypes`, resolved by
`KerberosEncryptionTypeList` as MIT's `krb5int_parse_enctype_list` does and kept to the
types the library has (BL-828, ADR-0209). For `--delegation`,
`GetForwardedTicketGrantingTicketAsync` turns a forwardable ticket-granting ticket into a
forwarded one by a TGS-REQ with `forwarded`, as MIT's `krb5_fwd_tgt_creds` does, and
refuses one without `forwardable` as `KerberosKdcError.TicketNotForwardable` (BL-831,
ADR-0210). It is the initiator of the GSS-API Kerberos V5 mechanism:
`KerberosGssContext` makes the initial context token (an AP-REQ whose authenticator
carries RFC 4121's checksum with the context flags, the MD5 of the caller's channel
bindings when given (RFC 2744's structure, as curl with MIT sends `tls-server-end-point`
over HTTPS; BL-832) and, for `--delegation`, a KRB-CRED of a forwarded ticket-granting
ticket), checks the acceptor's AP-REP, and then makes and
reads Wrap and MIC tokens, RFC 4121's for AES keys and RFC 4757's for `rc4-hmac`
(`KerberosGssContextOptions`, `KerberosGssFlags`, `KerberosDelegation`,
`KerberosGssToken` for RFC 2743's framing, `Rfc4121GssMessageProtection`,
`Rc4HmacGssMessageProtection`, `KerberosCredentialMessage`); every refusal is a
`KerberosGssException` with a `KerberosGssError` (BL-691, ADR-0171).

## Rules

- **Base class library plus `Curl.Cryptography.UnitLibrary` only.** No package, and no
  other project reference. `Curl.Cryptography.UnitLibrary` supplies AES-CBC-CTS, MD4
  and RC4 (BL-686) and the Camellia block cipher (BL-783).
- **Never a `Socket`.** The KDC is reached through an injected transport, so every
  exchange is testable with recorded bytes and no network.
- **Files through an injected seam.** The credential cache, the keytab and `krb5.conf`
  are read through an interface the caller supplies (`IKerberosFileReader`), and a
  credential cache is appended to through `IKerberosFileWriter`, never
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
