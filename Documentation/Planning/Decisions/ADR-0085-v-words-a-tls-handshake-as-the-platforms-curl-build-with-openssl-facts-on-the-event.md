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
- The handshake event carries no host name, so the `subjectAltName: ... matches` lines of
  `ossl_verifyhost`, the `SSL Trust` lines written at connect time, and the TLS record
  lines (`* TLSv1.3 (OUT), TLS handshake, ...`) that the OpenSSL build writes from
  `ReportTlsData` are left to follow-up tasks. `Proxy certificate:` is not yet
  distinguished from `Server certificate:`.

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
