# ADR-0305 — The Schannel build reports its client-certificate trust line before a `--cert` load failure

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1088.

## Context

curl 8.21.0's Schannel build words a `TlsTrustEvent` as two `schannel:` lines (BL-1083):
`schannel: disabled automatic use of client certificate` (or `enabled`), written by
`schannel_acquire_credential_handle`, and, for an IP-address host,
`schannel: using IP address, SNI is not supported by OS.`, written later by
`schannel_connect_step1`. Measured by BL-1083 with
`Record-CurlExchange.ps1 -Reset -CurlArgs -v,-k,--cert,nosuch.pem,https://127.0.0.1:18431/`:
curl writes the first line, then fails on the certificate with
`schannel: Failed to get certificate location or file for nosuch.pem` and exit 58, and never
writes the SNI line. Both of Curl's TLS providers reported the trust only after
`ClientCertificateLoader.Load` succeeded, so a load failure wrote no `schannel:` line.

## Decision

When the client certificate does not load, `SslStreamTlsProvider` and `HandBuiltTlsProvider`
call `SslStreamTlsProvider.ReportTrustBeforeClientCertificateFailure`, which in the Schannel
build reports the trust with `TargetsIpAddress` set to `false`, so the writer prints the
client-certificate line and no SNI line, before the exit 58 failure. The OpenSSL build reports
nothing, as before: its `SSL Trust` lines are not re-measured for this case, and the order
there stays as it was.

## Consequences

- The change stays inside `Curl.Networking.UnitLibrary`; `TlsTrustEvent` and
  `SchannelTrustText` are unchanged. `TargetsIpAddress` on that one event means the handshake
  never reached the point where the host's kind is reported.
- Every Schannel certificate failure (missing file, unreadable file, bad passphrase, store
  path not found) now writes the line first, as curl's credential-handle code does for all of
  them.

## Alternatives considered

- **A new `TlsTrustEvent` property saying the handshake stopped before SNI**, read by
  `SchannelTrustText`: lost because it widens a shared contract in
  `Curl.Protocol.Abstractions.UnitLibrary` and the output library for one line the existing
  property already expresses.
- **Report the full trust before loading the certificate**: lost because it would write the
  SNI line curl does not write, and would move the OpenSSL build's `SSL Trust` lines unmeasured.
