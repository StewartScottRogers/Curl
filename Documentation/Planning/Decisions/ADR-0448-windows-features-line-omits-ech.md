# ADR-0448 — `curl -V` on Windows does not list `ECH`

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1822, GF-0029).

## Context

BL-1417's audit added `ECH` to Curl's `Features:` line on every platform because Curl hand-builds
Encrypted Client Hello in its TLS 1.3 client (ADR-0233, ADR-0327). Gap finding GF-0029 measures
that as a gap on Windows: curl 8.21.0's Schannel reference build does not list `ECH` (the line
recorded in ADR-0444 has none), because Schannel offers no ECH.

## Decision

1. `CurlVersionText.WindowsFeaturesLine` drops `ECH`; `FeaturesLine` (Linux and macOS) keeps it.
2. `--ech` keeps working on every platform; only the `Features:` line changes.

## Why

- Matching the platform's curl is the standing rule: a script on Windows that reads `curl -V` must
  see what the Schannel build prints.
- Keeping `--ech` working changes nothing a script reading `curl -V` can see, and removing it would
  throw away working code.

## Consequences

- `features:ECH` measures `match` on Windows in the next gap analysis.
- This narrows, for Windows only, the `ECH` row BL-1417 added to ADR-0021.
