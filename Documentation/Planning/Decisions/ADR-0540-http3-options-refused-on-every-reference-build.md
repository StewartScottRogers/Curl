# ADR-0540 — `--http3` and `--http3-only` are refused on every platform unless the parse reads as a QUIC build

- **Status:** Accepted
- **Date:** 2026-10-15

Decided by Claude under Stewart's delegation (task BL-2332, GF-0089).

## Context

ADR-0397 made the Windows parse refuse the options curl's Schannel build lacks, `--http3` and
`--http3-only` among them, and ADR-0521 decision 2 kept both working on Linux and macOS over the
hand-built HTTP/3 and QUIC stack (ADR-0144). Gap finding GF-0089 measures that against the Linux
reference, curl 8.22.0 built from the release tarball with OpenSSL and no QUIC library (ADR-0505):
`curl --http3` answers `curl: option --http3: the installed libcurl version does not support this`
and the try-help line, exit 2, and so does `--http3-only`. macOS is built from the same tarball with
the same configure, so it answers the same. Every reference build therefore refuses both options.

The hand-built HTTP/3 stack is still Curl's: upstream's `http/3` cases run through it in the
conformance harness (ADR-0498), and Curl.Console's tests reach it through `--http3`.

## Decision

1. The `--http3` and `--http3-only` rows of `CommandLineOptionTable` are built with
   `CommandLineOption.RefusedWithoutQuic()`: they are refused with
   `CommandLineRefusal.InstalledLibcurlDoesNotSupport` on every platform unless the parse reads as a
   curl built with a QUIC library (`CommandLineOptions.ActsAsQuicBuild`), such as curl.se's ngtcp2
   build. Their `--no-` spellings stay refused as not reversible.
2. `CommandLineParser.Parse` gains an overload taking `actsAsQuicBuild`; `CurlCommandRunner` and both
   `CurlComposition.CreateRunner` overloads gain `parsesAsQuicBuild`, off by default. The executable
   never turns it on. Tests that pin the HTTP/3 stack, and the conformance harness, do.
3. `--ai-help` says both options are refused with exit 2 on every platform, in place of the
   Windows-only refusal line.
4. This supersedes ADR-0521 decision 2 and the part of ADR-0397 that ties the HTTP/3 refusal to the
   Schannel build.

## Why

- Matching the platform's curl is the standing rule, and every platform's reference build refuses
  both options.
- A parse-time switch keeps the hand-built HTTP/3 reachable and covered (the composition's QUIC
  dialer, the upstream `http/3` cases) without the shipped `curl` answering differently from the
  reference.
- A separate switch, not a third value of `isWindows`, because whether curl has a QUIC library is
  independent of the TLS backend `isWindows` stands for.

## Consequences

- `options:--http3` and `options:--http3-only` measure `match` on Linux and macOS in the next gap
  analysis, and still `match` on Windows.
- A script that passed `--http3` to Curl on Linux or macOS now gets exit 2, as it does with the
  reference curl there.
