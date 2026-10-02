# ADR-0331 — A TLS 1.0 or 1.1 ceiling connects through the hand-built client on every platform

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-714.

## Context

`--tls-max 1.0` and `--tls-max 1.1` already route to the hand-built TLS client
(ADR-0140's legacy-versions row, `TlsClientRouting`), which speaks TLS 1.0 and 1.1 with the
MD5/SHA-1 PRF and CBC records (BL-702, BL-703). BL-714 asked whether a range capped there
should connect to a legacy server, and measured real curl first.

Measured 2026-10-01 with `Record-CurlExchange.ps1 -NoServer -Curl <curl> -CurlArgs
'-sS','-k',<range>,'https://localhost:<port>/'` against `openssl s_server -tls1` (port
19410) and `openssl s_server -tls1_1` (port 19411), both `-cipher DEFAULT@SECLEVEL=0 -www`
(OpenSSL 3.5.7, Git for Windows). The Linux row ran inside WSL against Ubuntu's own
`s_server` the same way.

| Build | Range | Exit | stderr |
| --- | --- | --- | --- |
| curl 8.21.0, Schannel, Windows 11 (`C:\Windows\System32\curl.exe`) | `--tlsv1.0 --tls-max 1.0` | 0 | (none; the page, 3848 bytes) |
| same | `--tlsv1.0` | 0 | (none) |
| same | `--tlsv1.1 --tls-max 1.1` | 0 | (none) |
| same | `--tlsv1.1` | 0 | (none) |
| curl.se's Windows build, curl 8.18.0, LibreSSL 4.2.1 (WinGet `cURL.cURL`) | `--tlsv1.0 --tls-max 1.0` | 35 | `curl: (35) TLS connect error: error:1404E0BF:SSL routines:ST_BEFORE_CONNECT:no protocols available` |
| same | `--tlsv1.0` | 35 | `curl: (35) TLS connect error: error:1400442E:SSL routines:CONNECT_CR_SRVR_HELLO:tlsv1 alert protocol version` |
| same | `--tlsv1.1 --tls-max 1.1` | 35 | `curl: (35) TLS connect error: error:1404E0BF:SSL routines:ST_BEFORE_CONNECT:no protocols available` |
| same | `--tlsv1.1` | 35 | `curl: (35) TLS connect error: error:1400442E:SSL routines:CONNECT_CR_SRVR_HELLO:tlsv1 alert protocol version` |
| Ubuntu curl 8.18.0, OpenSSL 3.5.5 (WSL) | `--tlsv1.0 --tls-max 1.0` | 35 | `curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available` |
| same | `--tlsv1.0` | 35 | `curl: (35) TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version` |
| same | `--tlsv1.1 --tls-max 1.1` | 35 | `curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available` |
| same | `--tlsv1.1` | 35 | `curl: (35) TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version` |

So the task's premise was half wrong: curl.se's LibreSSL build does not connect (LibreSSL 4
has no TLS 1.0 or 1.1 left), and neither does OpenSSL 3 at its default security level. The
Schannel build does, on a Windows 11 whose Schannel still has TLS 1.0 and 1.1 enabled.

## Decision

A range capped at TLS 1.0 or 1.1 connects through the hand-built client on every platform,
as the Schannel build does: an official curl build connects, and the standing rule (Stewart,
2026-09-28) is that what any official build does, Curl does everywhere; a complete
reimplementation is never less capable than the most capable curl. The OpenSSL and LibreSSL
refusals are their libraries' security policy, not curl's behaviour, and a script that
names `--tls-max 1.0` is asking for the connection.

No production code changes: the routing row and the hand-built TLS 1.0 and 1.1 handshake
already existed. BL-714 adds `LegacyTlsTestServer`, an in-memory server that speaks only
TLS 1.0 or only TLS 1.1, and `HandBuiltTlsProviderTests.LegacyVersions.cs`, which completes
a request and response through the hand-built client against it as both builds, so the
answer holds on Windows, Linux and macOS alike.

A minimum alone (`--tlsv1.0`, `--tlsv1.1` with no `--tls-max`) stays on `SslStream`, which
connects where the operating system's stack still offers those versions (Windows, as
measured above) and fails where it does not; routing it is BL-1143's question, since it
changes the ClientHello of every such transfer to a modern server.

## Consequences

- On Linux and macOS, `--tls-max 1.0` against a TLS 1.0-only server exits 0 where the
  platform's OpenSSL curl exits 35: a deliberate, recorded difference in Curl's favour.
- The default range is untouched: `TlsClientRoutingTests` still pins it, and every range
  with a minimum below TLS 1.2 but no ceiling below it, on `SslStream`.

## Alternatives considered

- **Refuse with the OpenSSL build's `no protocols available` line on Linux and macOS.** Byte
  for byte the platform's curl there, but it makes Curl refuse a connection an official curl
  build makes, against the standing rule; and the refusal depends on the library's security
  level, which a distribution or `openssl.cnf` can change.
- **Refuse everywhere, as curl.se's LibreSSL build does.** Loses the Schannel build's answer
  on Windows, the platform whose curl the task's requirement names first.
