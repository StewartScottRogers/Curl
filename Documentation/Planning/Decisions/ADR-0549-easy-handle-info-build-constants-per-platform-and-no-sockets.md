# ADR-0549 — The easy handle's build-constant infos follow the platform's curl; no sockets, no early data

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2369.

## Context

BL-2361 gave `TransferInfo` (`Curl.Api.UnitLibrary`) the Schannel build's answers for the infos that
describe the build rather than the transfer: `CURLINFO_TLS_SSL_PTR`'s backend 8, no
`CURLINFO_CAINFO` or `CURLINFO_CAPATH`, and no `CURLINFO_SSL_ENGINES`. Curl matches the Schannel build
on Windows and the OpenSSL build on Linux and macOS, so off Windows those answers were the wrong build's.
Three transfer infos were also left undecided: `CURLINFO_LASTSOCKET`, `CURLINFO_ACTIVESOCKET` and
`CURLINFO_EARLYDATA_SENT_T`.

Measured 2026-10-15: curl 8.22.0's `acinclude.m4` (`CURL_CHECK_CA_BUNDLE`), with neither
`--with-ca-bundle` nor `--with-ca-path` given and Apple SecTrust off (it is opt-in,
`--with-apple-sectrust`), takes as `CURL_CA_BUNDLE` the first of `/etc/ssl/certs/ca-certificates.crt`,
`/etc/pki/tls/certs/ca-bundle.crt`, `/usr/share/ssl/certs/ca-bundle.crt`,
`/usr/local/share/certs/ca-root-nss.crt` and `/etc/ssl/cert.pem` that exists, and as `CURL_CA_PATH`
`/etc/ssl/certs` when that directory holds OpenSSL's hashed `XXXXXXXX.0` links. On Ubuntu (WSL, as on
the CI runner) the first bundle exists and `/etc/ssl/certs` holds the links; on macOS only
`/etc/ssl/cert.pem` exists and `/etc/ssl/certs` holds no links. Ubuntu's curl 8.18.0 with OpenSSL 3.5.5
lists `dynamic` for `--engine list`.

## Decision

- **`TlsSslBackend`, `CaInfo` and `CaPath` default to the platform's build constants**, chosen once per
  process by the internal `TlsBuildAnswers.Current`:
  - Windows: 8 (`CURLSSLBACKEND_SCHANNEL`), no bundle, no path.
  - Linux: 1 (`CURLSSLBACKEND_OPENSSL`), `/etc/ssl/certs/ca-certificates.crt`, `/etc/ssl/certs`.
  - macOS: 1, `/etc/ssl/cert.pem`, no path.

  They are the answers of the tarball built with `configure`'s defaults on the CI platforms (Ubuntu and
  macOS), the build the gap office measures against. A build constant does not probe the disk, as
  libcurl's does not: a distribution whose bundle lives elsewhere gets the same answer, as it would from
  a curl built on Ubuntu.
- **`SslEngines` stays empty on every platform.** ADR-0151 decided Curl has no crypto engines and its
  `--engine list` prints `<none>` everywhere; the info agrees with the tool rather than listing a
  `dynamic` engine Curl cannot load.
- **`LastSocket` and `ActiveSocket` answer -1 (`CURL_SOCKET_BAD`), before and after a transfer.** The
  engine's connections are managed `Socket`s behind `IConnection`, closed or pooled when the transfer
  ends, and the API hands out no operating-system descriptor a caller could use. Libcurl answers -1 when
  it has no socket to give, so -1 is the truthful answer until `CURLOPT_CONNECT_ONLY` work (ADR-0506
  plan phase 3) keeps a connection open for the caller, which revisits this.
- **`EarlyDataSent` answers 0.** Curl never sends TLS 1.3 early data (no `CURLSSLOPT_EARLYDATA`
  support yet), which is libcurl's answer when none was sent.

## Consequences

- Tests pin each platform's answers with `[OSCondition]` (`TransferInfoTests`) and every branch of
  `TlsBuildAnswers.For` on any platform (`TlsBuildAnswersTests`), so Windows-only coverage still reaches
  the Linux and macOS answers.
- Adding early data or connect-only support changes the matching answer and amends this ADR.
- Amended by ADR-0686 (BL-2633): while a `CURLOPT_CONNECT_ONLY` 1 connection is open, `LastSocket` and
  `ActiveSocket` answer its engine socket number, which `curl_multi_wait` polls.
