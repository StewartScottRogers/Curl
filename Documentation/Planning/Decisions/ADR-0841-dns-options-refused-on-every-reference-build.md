# ADR-0841 — `--dns-servers`, `--dns-interface`, `--dns-ipv4-addr` and `--dns-ipv6-addr` are refused on every platform unless the parse reads as a c-ares build

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3570, CGR-041).

## Context

BL-643 ran the four c-ares options through Curl's hand-built DNS resolver on every platform, and ADR-0454
decision 2 kept them accepted as an intended difference from the Schannel build. Issue CGR-041 of the
third-party gap report measures both reference builds, curl 8.22.0 Schannel on Windows and 8.18.0 OpenSSL
on Linux: neither is built with c-ares, so both answer `curl --dns-servers 127.0.0.1 -s <url>`,
`curl --dns-interface lo -s <url>` and `curl -sS --dns-ipv4-addr 127.0.0.1 <url>` with
`curl: option --dns-servers: the installed libcurl version does not support this` (the option as typed),
the try-help line and exit 2, sending nothing. Measured again here on 2026-10-26 with the local curl 8.21.0
Schannel: the same for all four options, for a blank value and for a missing one (the URL taken as the
value); `--no-dns-servers x` stays refused as not reversible. Curl accepted all four and ran the transfer.

This is ADR-0540's and ADR-0822's case again: a feature Curl has hand-built that no reference build has.

## Decision

1. The four rows of `CommandLineOptionTable` are built with `CommandLineOption.RefusedWithoutCAres()`:
   refused with `CommandLineRefusal.InstalledLibcurlDoesNotSupport` on every platform unless the parse
   reads as a curl built with c-ares (`CommandLineOptions.ActsAsCAresBuild`). The refusal comes after a
   missing value is refused as `requires parameter` and before the value is checked, so a blank value is
   refused as unsupported, not as blank.
2. `CommandLineParser.Parse`'s `actsAsQuicBuild` overload gains an optional `actsAsCAresBuild`. The
   executable never turns it on; tests of the options and of the resolver they drive (`CAresBuildParser`
   in `Curl.Cli.UnitTests`, the Engine and Console mapping tests) do.
3. `curl --ai-help` says each of the four is refused on every platform for want of c-ares.
4. This supersedes ADR-0454 decision 2 for the four DNS options; `--ech` is ADR-0822's.

## Consequences

- Scripts see what they see from either reference build: exit 2 and the same message.
- The hand-built resolver (`DnsServerResolver`, `DnsServerList`) stays, reachable through the c-ares
  parse and through `Curl.Api`, so a later decision to act as a c-ares build needs no new code.
