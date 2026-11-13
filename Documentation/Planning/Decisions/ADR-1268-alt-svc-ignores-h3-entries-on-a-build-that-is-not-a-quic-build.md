# ADR-1268: Alt-svc ignores h3 entries on a build that is not a QUIC build

- Status: Accepted
- Date: 2026-11-13
- Task: BL-5099 (outside fitness review finding STD-054)
- Decided by Claude under Stewart's delegation.

## Context

curl 8.18.0's OpenSSL build without QUIC (the Linux and macOS reference, ADR-0505, ADR-0540) ignores an
`h3` alt-svc entry, so a transfer with no version option goes to the origin over TCP. Curl offered h3 off
Windows (ADR-1238), so an unreachable h3 alternative ended in exit 28 at `-m`.

## Decision

`AltSvcTransferCache` takes `actsAsQuicBuild` (default `false`; the executable never sets it, as
`--http3` is refused the same way). With no version option off Windows the destinations are `h1` and
`h2`, plus `h3` only when the build acts as a QUIC build. A version option (`--http3`, `--http3-only`)
allows h3 as before (ADR-0141); Windows is unchanged (`h1` alone).
