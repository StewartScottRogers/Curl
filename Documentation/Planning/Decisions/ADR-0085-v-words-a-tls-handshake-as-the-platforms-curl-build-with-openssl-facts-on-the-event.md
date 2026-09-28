# ADR-0085 — `-v` words a TLS handshake as the platform's curl build, with OpenSSL's facts carried on the event

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-356.

## Context

ADR-0009 matches the Schannel build of curl on Windows and the OpenSSL build on Linux and
macOS. BL-228 made `VerboseTransferEventWriter` word a `TlsHandshakeEvent` only as
curl 8.21.0's Schannel build does: the two ALPN lines. The OpenSSL build prints far more
for the same handshake, measured in BL-356's Notes against a loopback `openssl s_server`:

```
* ALPN: curl offers h2,http/1.1
* SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS
* ALPN: server accepted http/1.1
* Server certificate:
*   subject: CN=localhost
*   start date: Sep 27 15:58:54 2026 GMT
*   expire date: Sep 27 15:58:54 2027 GMT
*   issuer: CN=localhost
*   Certificate level 0: Public key type RSA (2048/112 Bits/secBits), signed using sha256WithRSAEncryption
* OpenSSL verify result: 12
*  SSL certificate verification failed, continuing anyway!
```

Three things had to be decided: how the writers learn which build to match, where facts
that only the TLS library knows come from, and how much of OpenSSL's naming this tool
reproduces itself.

## Decision

- **The build is a `TlsBackend` (`Schannel` or `OpenSsl`) passed to the writer.**
  `VerboseTransferEventWriter` and `TraceTransferEventWriter` take it as a constructor
  argument; their older constructors choose `PlatformTlsBackend.ForProcess`, which is
  `ForPlatform(OperatingSystem.IsWindows())`. Tests name the backend explicitly, so the
  Schannel tests hold on Linux CI and the OpenSSL tests on Windows.
- **Facts only the TLS layer knows travel on `TlsHandshakeEvent`** as optional init
  properties, so no existing producer has to change: `NegotiatedGroupName`,
  `PeerSignatureTypeName`, `CertificateVerifyResult` (an OpenSSL `X509_V_` code) and
  `PeerCertificateChain`. The group cannot be derived (the same server negotiated
  `X25519MLKEM768` on TLS 1.3 and `x25519` on TLS 1.2). A missing group prints `[blank]`
  and a missing signature type `UNDEF`, curl's and OpenSSL's own fallbacks; a missing
  verify code is `0` when `CertificateVerified` is set and `1` (`X509_V_ERR_UNSPECIFIED`)
  when not; a missing cipher is `(NONE)`, as `SSL_get_cipher` says.
- **What can be derived from the certificate is derived in `Curl.Output`, as ports of
  OpenSSL.** `OpenSslDistinguishedNameText` ports `X509_NAME_print_ex` with curl's
  `x509_name_oneline` flags; `OpenSslSecurityBits` ports
  `ossl_ifc_ffc_compute_security_bits`; `OpenSslCertificateText` holds OpenSSL's names for
  signature algorithms, key types and named curves, and `ASN1_TIME_print`'s date form;
  `OpenSslHandshakeText` holds `SSL_get_version`'s names and OpenSSL's names for the TLS
  1.2 suites it enables by default (from `openssl ciphers -stdname DEFAULT`, OpenSSL
  3.5.7). A suite outside that table, and every TLS 1.3 suite, prints its IANA name, which
  is OpenSSL's name for the TLS 1.3 suites.
- **Where this tool cannot know what OpenSSL would print, it prints less rather than
  guess.** A key other than RSA, RSA-PSS, EC on a curve in the table, Ed25519 or Ed448
  gets no `Certificate level` line. A name OpenSSL could not print is `[NONE]` for the
  issuer as well as the subject (curl fails the transfer on an unprintable issuer, which
  only a malformed certificate causes).
- The host-name lines of `ossl_verifyhost`, the `SSL Trust` lines, the TLS record lines
  (`* TLSv1.3 (OUT), TLS handshake, ...`) and `Proxy certificate:` were outside this
  decision. `Curl.Output` now writes all of them for `-v` (amendment BL-405), and
  `--trace` writes the record and trust lines too (BL-450). `Curl.Networking` reports the
  trust event, the checked host name and proxy handshakes (amendment BL-452); what it
  still does not report is the TLS records themselves, which `SslStream` does not expose.

## Consequences

- Producers (`Curl.Networking`'s `SslStream` provider) can fill the new properties one at
  a time; each one filled makes the OpenSSL wording more exact, and none is required.
- The OpenSSL wording lives entirely in `Curl.Output`, next to the Schannel wording, so a
  new measurement changes one library.
- The name tables are finite. A new signature algorithm, curve or cipher suite prints its
  dotted OID, is skipped, or prints its IANA name until a row is added.

## Alternatives considered

- **`OperatingSystem.IsWindows()` inside the writer.** Lost: the Windows run could not
  test the OpenSSL wording, nor Linux CI the Schannel wording, and 100% branch coverage
  would fail on either.
- **Carrying every printed string on the event (subject text, level lines).** Lost: every
  producer would have to reimplement OpenSSL's printing, and `SslStream` exposes none of
  it; the certificate itself is already on the event.
- **Deriving the verify code from `CertificateVerified` alone.** Lost: curl prints the
  exact code (`12`, hex, for a self-signed certificate), which only the TLS layer knows.

## Amendment (BL-404, 2026-09-27): `SslStreamTlsProvider` reports the event

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

- **Who reports it.** `SslStreamTlsProvider` reports a `TlsHandshakeEvent` after every
  successful handshake through a four-argument `AuthenticateAsClientAsync` overload that
  takes the `ITransferEvents`; the `ITlsProvider` overload reports to `NoTransferEvents`.
  `TcpConnector` passes `ConnectTarget.Events` to any provider implementing the internal
  `IHandshakeReportingTlsProvider`, so `ITlsProvider` in Abstractions is unchanged. This
  amendment left the HTTPS proxy's handshake unreported, because `-v` could not yet word
  it as `Proxy certificate:`; amendment BL-452 reports it.
- **What it fills.** Version, cipher suite, the server's certificate, whether it verified,
  `CertificateVerifyResult` and `PeerCertificateChain`: the chain .NET built when the chain
  verified (errors cleared by a `--capath` root count as verified), else what the server
  sent. No ALPN is offered, so none is reported. `SslStream` exposes neither the
  key-exchange group nor the peer signature type; both stay `null` and print as
  OpenSSL's fallbacks.
- **The `X509_V_` mapping** (`OpenSslVerifyResult`), from what the validation callback saw:

  | `SslStream` saw | Code |
  | --- | --- |
  | No certificate sent | `null` |
  | No chain error (a host-name mismatch alone included: curl checks the name itself after OpenSSL verified) | `0` `X509_V_OK` |
  | `NotTimeValid`, a certificate's start date still ahead | `9` `X509_V_ERR_CERT_NOT_YET_VALID` |
  | `NotTimeValid` otherwise | `10` `X509_V_ERR_CERT_HAS_EXPIRED` |
  | `UntrustedRoot`, chain of one | `18` `X509_V_ERR_DEPTH_ZERO_SELF_SIGNED_CERT` |
  | `UntrustedRoot`, longer chain | `19` `X509_V_ERR_SELF_SIGNED_CERT_IN_CHAIN` |
  | `PartialChain` | `20` `X509_V_ERR_UNABLE_TO_GET_ISSUER_CERT_LOCALLY` |
  | Any other status | `null` |

  A date error wins over a trust error because OpenSSL's `verify_chain` checks dates in
  `internal_verify`, after `build_chain` reports the trust error, and
  `SSL_get_verify_result` keeps the last error. This was read from OpenSSL's source, not
  measured: this Windows checkout runs the Schannel build, which prints none of it.

## Amendment (BL-405, 2026-09-27): TLS message, trust and host-name lines

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"). Measured
against curl 8.21.0's OpenSSL build (`curlimages/curl:8.21.0`); the measurements are in
BL-405's Notes.

- **New facts ride new, optional contract members, so no other project changes.**
  `ITransferEvents` gained two default interface members: `ReportTlsMessage(TlsMessageEvent)`
  (OpenSSL's message callback: version number, `TlsContentType`, direction, bytes; by
  default it forwards the bytes to `ReportTlsData`) and `ReportTlsTrust(TlsTrustEvent)`
  (`VerifiesPeer`, `HasCaCertificateBlob`, `CaCertificateFile`, `CaCertificateDirectory`;
  a no-op by default). `TlsHandshakeEvent` gained `IsProxy` and `VerifiedHostName` (the
  host as typed, `null` when the host is not checked). Changing `ReportTlsData`'s
  signature was rejected: it would break every implementation and fake of
  `ITransferEvents` in other projects.
- **The Schannel wording writes nothing for the new events**, as curl's Schannel build
  has no message callback and no `SSL Trust` lines. The OpenSSL wording writes the TLS
  record lines (`OpenSslMessageText`) and the `SSL Trust` lines (`OpenSslTrustText`), and
  writes TLS bytes as `{`/`}` data lines under the same `traced_data` rule as body bytes.
  `IsProxy` makes the certificate block say `Proxy certificate:` instead of
  `Server certificate:`.
- **Host-name checking is ported, not reported.** `OpenSslHostNameText` re-runs
  `ossl_verifyhost` on the certificate the event carries (the SAN entries of the host's
  kind, else the last single-valued CN; RFC 6125 wildcards as `hostcheck.c`), because the
  line names the matching SAN entry, which `SslStream` never exposes. A failed check
  writes only curl's info line (or none) and stops before the verify result; the error
  text is the transfer's.
- **Defaults taken.** A CN in a multi-valued RDN is skipped (OpenSSL would find it; rare).
  `CURL_CA_FALLBACK`, native CA stores and the "error setting certificate file" variants
  are not worded, as the measured Linux build prints none of them. IPv4 host detection
  counts dots, where curl uses `inet_pton`.

## Amendment (BL-452, 2026-09-27): `Curl.Networking` reports trust, host name and proxy handshakes

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

- **The proxy flag rides the internal interface.** `IHandshakeReportingTlsProvider`'s
  method gained `bool isProxy`; `SslStreamTlsProvider` keeps its public four-argument
  overload (`isProxy: false`) and adds a five-argument one. `TcpConnector` reports the
  HTTPS tunnelling proxy's handshake and a forward proxy's with `isProxy: true`, as curl's
  OpenSSL build says `Proxy certificate:` for both. Both land on the target's `Events`.
- **Trust is reported after the cipher and client-certificate checks, before the
  `--cacert` file is read**, so exit 59 and exit 58 report no trust (curl fails those in
  `ossl_connect_step1`, before the store is populated) while a bad `--cacert` still shows
  the `SSL Trust Anchors:` lines it was loading. The provider reports it on every
  platform; `Curl.Output` writes nothing for it under Schannel.
- **The default bundle is named `/cacert.pem`**, the reference build's, held as
  `SslStreamTlsProvider.OpenSslDefaultCaCertificateFile`. It is named, never read:
  verification without `--cacert` still uses the system store. Distribution builds print
  their own path; matching the measured reference build is the standing rule. With only
  `--capath` the default file is still named, as curl keeps its built-in bundle unless
  `--cacert` replaces it.
- **`VerifiedHostName`** is the host as passed to the handshake, an IPv6 literal without
  brackets, and `null` under `-k` (or `--proxy-insecure` for the proxy's handshake).
- **No ALPN is offered**, so no `ALPN: curl offers` line is written and its ordering
  needs no decision yet; whoever adds ALPN places the offer line.
- **No `TlsMessageEvent` is reported**: `SslStream` exposes no TLS records, so the record
  lines stay unwritten in a real transfer.
