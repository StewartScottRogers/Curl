# ADR-0282 — `%{ssl_verify_result}` and `%{proxy_ssl_verify_result}` print the OpenSSL verify code off Windows

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-661.
Amends ADR-0043 for these two variables off Windows.

## Context

ADR-0043 printed both variables as a constant `0`, which is what curl 8.21.0's Schannel build
prints even for a failed verification. curl's OpenSSL build prints `SSL_get_verify_result`
instead. Measured 2026-09-30 on Ubuntu's curl 8.18.0 (OpenSSL 3.5.5) against
`openssl s_server -www`:

| Case | Printed | Exit |
| --- | --- | --- |
| `-k`, self-signed `localhost` | `18` | 0 |
| `--cacert` for it | `0` | 0 |
| no option | `18` | 60 |
| `-k`, expired self-signed | `18` | 0 |
| `--cacert` for the expired one | `10` | 60 |
| leaf of an untrusted CA sent with it, `-k` or not; leaf only | `20` | 0 / 60 |
| expired leaf of that CA, `--cacert` for the CA | `10` | 60 |
| `--cacert` trusted, host name not in the certificate | `1` | 60 |
| same with `-k` | `20` | 0 |
| `--proxy-insecure -x https://` self-signed proxy, `http://` URL | `0 18` | 0 |
| `http://` URL | `0` | 52 |

So a trust error is reported before a date error (as BL-150 measured for the `-v` text), and
once curl's own host name check refuses the certificate the code is `1`
(`X509_V_ERR_UNSPECIFIED`), whatever the chain.

## Decision

- `ITransferEvents` gains `ReportCertificateVerifyResult(long, bool isProxy)`, a no-op by
  default. `SslStreamTlsProvider` and `HandBuiltTlsProvider` report it once the certificate was
  judged, whether the handshake went on or failed (`PeerVerification.ReportVerifyResult`);
  `QuicDialer` reports it for a connection it opens. A code `OpenSslVerifyResult` cannot map is
  reported as `1`. The Schannel build reports nothing, so Windows keeps printing `0`.
- `OpenSslVerifyResult.OfChainStatus` checks `UntrustedRoot` and `PartialChain` before
  `NotTimeValid`, as measured, and `ServerCertificateVerification` gives `1` for a name
  mismatch unless `-k`. This also corrects the `-v` verify line for an expired untrusted
  certificate.
- `Curl.Console` wraps a transfer's events in `VerifyResultRecordingTransferEvents` when the
  option group has `-w`; it keeps the origin's and the proxy's last code on the
  `RunningTransferState`, and `TransferWriteOutVariables.SslVerifyResult` and
  `ProxySslVerifyResult` print them. A transfer that reported none prints `0`.

## Why

An event beside `ReportTlsHandshake` is the one path that carries the code out of a failed
handshake too: a `TlsHandshakeEvent` is reported only for a completed one, and a
`ConnectResult` or `TransferReport` member would have needed every protocol handler to copy it.
Recording only under `-w` keeps every other transfer's event sink as it was.

## Left as it is

- An FTPS data connection's handshake goes through `Curl.Protocol.Ftp`'s `FtpDataConnectEvents`,
  which does not forward the new event, so the control connection's code is printed.
- `--crlfile` refusals report the chain's code, not the revocation list's (for example 23).
- A QUIC handshake refused by verification reports no code.
