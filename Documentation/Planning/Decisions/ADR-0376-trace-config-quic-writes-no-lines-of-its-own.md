# ADR-0376 — `--trace-config quic` writes no lines of its own

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1169.

## Context

ADR-0318 lists `quic` among the protocol components of `--trace-config` and BL-1169 was filed to
write curl's `* [QUIC] ...` lines from `Curl.Quic.UnitLibrary`. The Schannel build curl.exe 8.21.0
has no HTTP/3, so the measurement uses curl.se's Windows build 8.18.0 (LibreSSL 4.2.1, ngtcp2
1.21.0, nghttp3 1.15.0), the HTTP/3 reference ADR-0144 names, against
`https://cloudflare-quic.com/` with `--http3-only` (BL-1169 Notes):

- `-v --trace-config quic` writes exactly the `* [...]` lines `-v` alone writes - the
  `[HTTP/3]` stream-open and request-header lines - and no `[QUIC]` line;
- `network` writes `[DNS]`, `[HAPPY-EYEBALLS]` and `[MULTI]` lines, and `all` adds
  `[UDP] QUIC socket ... connected` and the timer component's `[TIMER] [QUIC] set for ...` lines,
  but no line tagged `[QUIC]` either: in that build the QUIC transport's own trace is the
  `[HTTP/3]` filter's (`http/3`, ADR-0375), and `QUIC` is only the name of a timer.

## Decision

`--trace-config quic` is accepted, as it is today, and writes nothing beyond `-v`, matching the
measured build. `Curl.Quic.UnitLibrary` gets no trace sink. Tests in
`CurlCommandRunnerHttp3Tests` pin that an HTTP/3 GET under `-v --trace-config quic` writes
exactly the `-v` lines, that `network`, `protocol`, `all` and `-vvvv` write no `[QUIC]` line, and
that `quic` without `-v` writes nothing.

## Consequences

- Nothing to build; a later curl that adds `[QUIC]` lines is a new task once measured.
- The `[UDP]` socket line and the `[TIMER]` lines belong to the `network` and `timer`
  components, not to `quic`, and are left to their own tasks.
