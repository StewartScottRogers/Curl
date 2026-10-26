# ADR-0822 — `--ech` is refused on every platform unless the parse reads as an ECH build

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3571, CGR-042).

## Context

ADR-0327 ran `--ech` through the hand-built TLS 1.3 client on every platform, and ADR-0520 decision 2
kept it working when `ECH` left the `Features:` line, since no reference build lists it. Issue CGR-042 of
the third-party gap report measures the option itself: curl 8.22.0's Schannel build answers
`curl --ech true -s <url>` and `curl -sS --ech 0 -o nUL <url>` with
`curl: option --ech: the installed libcurl version does not support this` and the try-help line, exit 2,
nothing on standard output and nothing sent. Measured again here on 2026-10-26 with
`Record-CurlExchange.ps1`. curl's `tool_getparam.c` checks `feature_ech` before it reads the value, so
every value, blank included, is refused the same way. The OpenSSL reference built from the release
tarball (ADR-0505) has no ECH either (ADR-0520), so it answers the same. Curl accepted the option, ran
`--ech true` and failed `--ech 0` with exit 43 `setopt 0x2855 got bad argument`.

This is ADR-0540's case for `--http3` again: a feature Curl has hand-built that no reference build has.

## Decision

1. The `--ech` row of `CommandLineOptionTable` is built with `CommandLineOption.RefusedWithoutEch()`: it is
   refused with `CommandLineRefusal.InstalledLibcurlDoesnotSupport` on every platform unless the parse reads
   as a curl built with ECH (`CommandLineOptions.ActsAsEchBuild`).
2. `CommandLineParser.Parse`'s `actsAsQuicBuild` overload gains an optional `actsAsEchBuild`;
   `CurlCommandRunner` and both `CurlComposition.CreateRunner` overloads gain `parsesAsEchBuild`, off by
   default. The executable never turns it on. Tests that pin the ECH code, and the upstream conformance
   harness, do.
3. `--ai-help` says the option is refused with exit 2 on every platform.
4. The CLI/HTTP differential case `ech` loses its `noEch` limitation: Curl and the reference now agree.
5. This supersedes ADR-0520 decision 2 and the part of ADR-0327 that makes `--ech` reachable from the
   shipped `curl`.

## Why

- Matching the platform's curl is the standing rule, and every reference build refuses the option.
- A parse-time switch keeps the hand-built ECH (ADR-0233, ADR-0327, ADR-0378) reachable and covered without
  the shipped `curl` answering differently from the reference; nothing is removed.
- A separate switch from `ActsAsQuicBuild`, because whether curl was built with ECH is independent of
  whether it has a QUIC library.

## Consequences

- A script that passed `--ech` to Curl now gets exit 2, as it does with the reference curl.
- `--help` still lists `--ech`, as curl's does.
