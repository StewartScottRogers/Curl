# ADR-0191 — `--cert-status` runs the hand-built client and `--ssl-auto-client-cert` reads the personal store, on every platform

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-610.
This records HOW the two options are honoured, not WHETHER: the standing rule is that a
feature any official curl build supports is supported on every platform.

## Context

BL-607 parses `--cert-status` (`CommandLineOptions.RequireCertificateStatus`),
`--ssl-auto-client-cert` (`AutoClientCertificate`) and `--proxy-ssl-auto-client-cert`
(`ProxyAutoClientCertificate`). BL-705 made the hand-built TLS 1.3 and 1.2 clients ask for,
verify and report a stapled OCSP response (`OcspStapleVerifier`, `OcspStapleOutcome`,
`TlsHandshakeFailure.CertificateStatusRejection`, ADR-0173). `SslStream` exposes no stapled
response.

Measured on 2026-09-29 against `openssl s_server -status_file <resp>` (a throwaway CA, a leaf
for `localhost`, responses made with `openssl ocsp -index`), stderr and exit code:

| Build | good | revoked (keyCompromise) | none stapled |
| --- | --- | --- | --- |
| curl 8.18.0, OpenSSL 3.5.5 (Ubuntu, WSL) | exit 0 | `curl: (91) SSL certificate revocation reason: keyCompromise (1)` | `curl: (91) No OCSP response received` |
| curl.se's Windows build 8.18.0, LibreSSL 4.2.1 (with `--cacert`) | exit 0 | same text, exit 91 | same text, exit 91 |
| curl 8.21.0 Schannel (`C:\Windows\System32` and the MSYS2 build) | exit 0 | exit 0 | exit 0 |

The OpenSSL build also gave `(UNKNOWN) (-1)` for a revocation with no reason, each of OpenSSL's
reason names (`unspecified (0)` to `removeFromCRL (8)`), `SSL server certificate status
verification FAILED` for an unknown status, `Invalid OCSP response status: trylater (3)`, and
`OCSP response verification failed` for a response signed by another CA. With `-v` it prints
`* SSL certificate status: good (0)` (or `revoked (1)`). With `-k` a missing response is still
exit 91. The Schannel build ignores `--cert-status` silently.

`--ssl-auto-client-cert` against `s_server -Verify 1`: the Schannel build presented a
certificate from `CurrentUser\MY` (one with no extended key usage) and printed
`* schannel: enabled automatic use of client certificate` under `-v`; the OpenSSL build and
curl.se's LibreSSL build ignore the option and fail with exit 56 (`tlsv13 alert certificate
required`).

Exit 83 (`CURLE_SSL_ISSUER_ERROR`) comes only from libcurl's `CURLOPT_ISSUERCERT`; curl
8.21.0's command line has no option that sets it (`CurlManual.txt` names no issuer
certificate option), so no Curl option produces exit 83 and none should.

## Decision

1. **`--cert-status` is a routing row.** `TlsClientOptions.RequireCertificateStatus` sends the
   connection to `HandBuiltTlsProvider` on every platform (`TlsClientRouting.Choose`), which
   sets `RequestOcspStatus` and its `TimeProvider` on the TLS 1.3 or 1.2 client. The origin
   only: curl has no proxy form of the option.
2. **One text on every platform: OpenSSL's.** A rejected response is exit 91 with the text
   `CertificateStatusFailureMessages.For` gives each `OcspStapleOutcome` - curl's
   `verifystatus()` strings, with OpenSSL's CRL-reason and response-status names and
   `(UNKNOWN)` for a code it has no name for. The Schannel build's silence is not matched,
   because the official Windows build from curl.se does check and prints the same text.
3. **`--ssl-auto-client-cert` reads the user's personal store on every platform.**
   Without `--cert`, `ClientCertificateLoader.Load` asks `AutomaticClientCertificate.Choose`,
   which opens `CurrentUser\MY` through `IClientCertificateStore` (`X509Store`: the Windows
   store, .NET's user store on Linux, the keychain on macOS) and takes the first certificate
   that has its private key, is valid now, and has no extended key usage or one naming client
   authentication or any usage; the rest are disposed. The certificate is presented only
   when the server asks, as `--cert`'s is, by both providers. `--cert` wins when both are
   given. `--proxy-ssl-auto-client-cert` does the same for the HTTPS proxy's handshake.
4. **The choice is made before the handshake, not from the server's CA list.** The
   hand-built client takes its certificate up front, so both providers choose without the
   `certificate_authorities` the server names. Schannel filters by them; with one qualifying
   certificate in the store the result is the same.
5. **No `-v` lines yet.** `* SSL certificate status: ...` and the Schannel build's
   `automatic use of client certificate` line need the handshake event to carry them; that is
   follow-up work (filed from BL-610).

## Consequences

- `--cert-status` fails a transfer whose server staples nothing, even on Windows where the
  system curl would succeed; this is curl.se's Windows build's behaviour.
- A `--cert-status` transfer runs the hand-built TLS stack with its default suites and groups
  (ADR-0162), so its `-v` handshake lines may differ from an `SslStream` transfer's.
- Off Windows `--ssl-auto-client-cert` presents a certificate where the OpenSSL build of curl
  presents none; with an empty store (the usual case) the result is the same.

## Alternatives considered

- **Match the Schannel build on Windows (ignore `--cert-status`).** Rejected by the standing
  rule: an official build supports it, so every platform does.
- **Select the automatic certificate inside `SslStream`'s selection callback, filtered by the
  server's acceptable issuers.** Closer to Schannel for `SslStream`, but the hand-built path
  cannot do the same without a change to `Curl.Tls`, and two different choices for one option
  would depend on which provider the routing picked.
- **Let `SslStream` on Windows pick the credential itself.** .NET disables Schannel's default
  credentials, and BL-254 showed its credential cache leaks a certificate into later
  handshakes; the explicit choice avoids both.
