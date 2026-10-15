# ADR-0521 — `curl -V` lists `HTTP3` on no platform

- **Status:** Accepted
- **Date:** 2026-10-15

Decided by Claude under Stewart's delegation (task BL-2330, GF-0087).

## Context

ADR-0144 added `HTTP3` to the `Features:` line once `--http3` and `--http3-only` worked, and ADR-0451
dropped it from the Windows line only, keeping it on Linux and macOS on the premise that the reference
build there lists it. Gap finding GF-0087 measures that premise false: curl 8.22.0 built from the release
tarball with OpenSSL (OpenSSL/3.0.13 on the Linux runner) has no QUIC library - no ngtcp2, quiche or
OpenSSL QUIC on its version line - and prints no `HTTP3` on its `Features:` line. curl lists `HTTP3`
only when built with a QUIC library, which a default build, the one the platform measurement builds on
Linux and macOS alike (ADR-0505), is not.

## Decision

1. `CurlVersionText.FeaturesLine` (Linux and macOS) drops `HTTP3`, as `WindowsFeaturesLine` already did.
2. `--http3` and `--http3-only` keep working on every platform over the hand-built HTTP/3 and QUIC
   stack (ADR-0144); only the `Features:` line changes.
3. This supersedes ADR-0451's premise that Linux and macOS list `HTTP3`, and ADR-0144's addition of
   `HTTP3` to the line.

## Why

- Matching the platform's curl is the standing rule, and the measured reference does not list `HTTP3`.
- macOS is built from the same tarball with the same default configure, so its line agrees; the
  shared `FeaturesLine` needs no platform split.
- Removing HTTP/3 would throw away working code and change nothing a script reading `curl -V` sees.

## Consequences

- `features:HTTP3` measures `match` on Linux and macOS in the next gap analysis.
- A script that probes `curl -V` for `HTTP3` before passing `--http3` now skips it, as it would with curl.
