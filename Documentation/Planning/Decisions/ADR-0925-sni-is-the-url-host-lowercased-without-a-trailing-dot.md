# ADR-0925: SNI is the URL host lowercased, without a trailing dot

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3622, CGR-093)

## Context

Third-party gap report finding CGR-093 found that `SslStreamTlsProvider` handed the URL host to
`SslClientAuthenticationOptions.TargetHost` as written, so `https://LOCALHOST:p/` sent the SNI
`LOCALHOST` and, on Linux, `https://localhost.:p/` sent `localhost.`, a name RFC 6066 section 3
forbids in SNI. curl lowercases the URL host when it parses the URL and strips a trailing dot from
the name it sends, while the `Host` header keeps the case written.

Measured on 2026-10-26 on Windows against an SNI-logging C# `SslStream` server, curl 8.21.0
(Schannel, `C:\Windows\System32\curl.exe`) and Curl with this change, `curl -sS -k https://H:p/`:

| H | SNI real curl sent | SNI Curl sends | Host header (both) |
| --- | --- | --- | --- |
| `LOCALHOST` | `localhost` | `localhost` | `LOCALHOST:p` |
| `LocalHost` | `localhost` | `localhost` | `LocalHost:p` |
| `localhost.` | `localhost` | `localhost` | `localhost.:p` |

The report measured the same `localhost` for curl 8.18.0 (OpenSSL) on Linux.

## Decision

`SslStreamTlsProvider.ServerNameIndication` gives the handshake's `TargetHost`: the target host
lowercased (invariant culture), with one trailing dot removed. It applies to both builds, since
both curls send the same name. Certificate verification, the trust event and the verified host
name still take the host as given; host name matching is case-insensitive and the BCL's name
check now sees the dot-free name. The hand-built provider (`HandBuiltTlsProvider`) is not
changed by this decision.

## Consequences

The SNI byte string matches curl's for any host case and for a fully qualified name; servers that
choose a certificate by SNI choose the same one for Curl as for curl.
