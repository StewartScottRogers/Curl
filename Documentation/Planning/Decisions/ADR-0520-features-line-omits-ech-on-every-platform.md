# ADR-0520 — `curl -V` lists `ECH` on no platform

- **Status:** Accepted
- **Date:** 2026-10-15

Decided by Claude under Stewart's delegation (task BL-2329, GF-0086).

## Context

ADR-0448 dropped `ECH` from the Windows `Features:` line and kept it on Linux and macOS, on the
premise that the OpenSSL reference build lists it. Gap finding GF-0086 measures that premise false:
curl 8.22.0 built from the release tarball with OpenSSL (OpenSSL/3.0.13 on the Linux runner) prints no
`ECH` on its `Features:` line. curl builds ECH only when asked to (`--enable-ech` / `USE_ECH`, still
marked experimental, and only with a TLS library that has ECH), so a default build, which is what the
platform measurement builds on Linux and macOS alike (ADR-0505), never lists it.

## Decision

1. `CurlVersionText.FeaturesLine` (Linux and macOS) drops `ECH`, as `WindowsFeaturesLine` already did.
2. `--ech` keeps working on every platform, through the hand-built TLS 1.3 client (ADR-0233,
   ADR-0327); only the `Features:` line changes.
3. This supersedes ADR-0448's Decision 1 for Linux and macOS.

## Why

- Matching the platform's curl is the standing rule, and the measured reference does not list `ECH`.
- macOS is built from the same tarball with the same default configure, so its line agrees; the
  shared `FeaturesLine` needs no platform split.
- Removing `--ech` would throw away working code and change nothing a script reading `curl -V` sees.

## Consequences

- `features:ECH` measures `match` on Linux and macOS in the next gap analysis.
- A script that probes `curl -V` for `ECH` before passing `--ech` now skips it, as it would with curl.
