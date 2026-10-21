# ADR-0720: The harness reports `http/3` to a case naming its `http/3` stand-in server

- Status: Accepted
- Date: 2026-10-21
- Decided by Claude under Stewart's delegation (BL-2838)

## Context

ADR-0540 has Curl refuse `--http3` and `--http3-only` as every reference build does (none has a QUIC
library), so BL-2370 took `http/3` off every `UpstreamCurlPlatform` and upstream cases 2500 to 2503
skipped with `Curl lacks the feature http/3`. Curl still has its hand-built QUIC and HTTP/3 stack, the
conformance tests compose curl as a QUIC build (`CreateRunner(parsesAsQuicBuild: true)`), and the harness
has the stand-in for upstream's nghttpx-h3 server (ADR-0498, ADR-0502). The root `CLAUDE.md` says nothing
is left out because it is hard, so the stack should be measured against upstream's cases.

2501 also names `Debug`. Its `<setenv>` is empty and its command (`--insecure --http3 ... -d moo`) uses
nothing debug-only: it names `Debug` only because upstream runs its http/3 cases on debug builds, as the
`%HTTP2TLSPORT` cases do (ADR-0497).

Measured on Windows on 2026-10-21 with the harness reporting `http/3`: 2501 (`--insecure`) passes; 2500,
2502 and 2503 end with exit 60, `schannel: the revocation status is unknown`, because Curl matches the
Schannel build there, which checks revocation under `--cacert` and the generated test CA publishes none.
With that check off, as in the OpenSSL build Curl matches on Linux and macOS, all four pass.

## Decision

1. `http/3` stays off every `UpstreamCurlPlatform`, consistent with ADR-0540: `curl --version` and the
   features any other case sees are unchanged.
2. The conformance tests (`UpstreamConformanceTests.WithHttp3StandIn`) report `http/3` and `Debug` to a
   case whose `<server>` part names the `http/3` server, the one the harness stands in for, and to no
   other case. Upstream's `runtests.pl` reports `http/3` when its curl has HTTP/3 and its nghttpx-h3 runs;
   here both hold for the harness's curl and stand-in.
3. 2500, 2501, 2502 and 2503 go on `PassingUpstreamCases.txt`. On Windows the ratchet runs them but does
   not hold them (`IsHeldOnThisPlatform`), so they are `Inconclusive` there, as ADR-0498 runs the http/3
   cases off Windows only; Linux and macOS judge them in CI.

## Why

- It measures the hand-built HTTP/3 stack through upstream's own cases without changing what the shipped
  `curl` reports, the same harness-stand-in pattern as `Debug` (ADR-0497) and `override-dns` (ADR-0492).
- Not holding them on Windows matches the Schannel build's revocation check rather than weakening it for
  a test; the OpenSSL build's answer is the one these cases expect.

## Consequences

- The gap office's scorecard is unaffected: the reference curl still lacks `http/3`, so the cases stay
  `excluded` there.
- A change that breaks HTTP/3 over the stand-in turns CI red on Linux and macOS.
