# ADR-0859 — TLS sessions resume only between matching verification settings

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3693, CGR-164).

## Context

CGR-164 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) found that Curl resumed a TLS session
across `--next` transfers whose verification settings differ: `-k` then `--cacert`, the reverse, and
an added `--pinnedpubkey`. curl keys its session cache on the peer's SSL configuration (verify mode,
CA, pin, client certificate), so it makes a new session in each of those cases and resumes only
between transfers with the same settings.

`SslStreamTlsProvider` runs every default-route handshake through `SslStream`, whose session cache is
process-wide and keyed on the target host and the credential (protocols, client certificate,
revocation mode, `AllowTlsResume`), never on how the peer is verified: the certificate callback is
not part of it. No `SslStream` setting names a cache partition.

Measured on Windows against `openssl s_server -www` (`New,`/`Reused,` per connection), TLS 1.2 and
TLS 1.3, with curl 8.21.0 Schannel (`C:\Windows\System32\curl.exe`): `-k`/`--cacert`, `--cacert`/`-k`
and `--cacert`/`--cacert --pinnedpubkey` are `New, New`; `-k`/`-k` is `New, Reused`; `-k`, `--cacert`,
`--cacert --pinnedpubkey` is `New, New, New`; `-k`, `--cacert`, `--cacert` is `New, New, Reused`.

## Decision

- `SslStreamSessionPartitions` records, per target host (ignoring case), the verification settings of
  the first handshake to it: `-k`, the libcurl host check under it, `--cert-status`, the CA file's full
  path, the CA blob's SHA-256, the CA directory, `--pinnedpubkey`, and the client certificate's and
  key's full paths. A later handshake to that host with the same settings resumes as before; one with
  other settings sets `SslClientAuthenticationOptions.AllowTlsResume` to `false`, which gives it a
  credential of its own in `SslStream`'s cache and so a full handshake.
- One instance serves the process (`SslStreamSessionPartitions.Process`), as `SslStream`'s cache does;
  `SslStreamTlsProvider.SessionPartitions` is an internal `init` seam so each test has its own.
- The hand-built provider keeps its own `TlsSessionCache` keyed by `TlsSessionCache.PeerKey`, unchanged.

## Consequences

- Every CGR-164 repro and the control match curl on both TLS versions.
- A connection that did not own its host's sessions is never resumed: `-k`, `--cacert`, `--cacert`
  gives `New, New, New` where curl gives `New, New, Reused`. Only the handshake count differs, never a
  transfer's output; `SslStream` offers no further partitions to close it.
