# ADR-0495: The harness reports http/2 off Windows only, so the h2c cases run on Linux and macOS

- Status: Accepted
- Date: 2026-10-14
- Decided by Claude under Stewart's delegation (BL-2230)

## Context

BL-2230 puts the h2c stand-in for nghttpx (ADR-0491) on `%HTTP2PORT` through `UpstreamHttp2Server`,
a record the caller passes to `UpstreamCaseRunner`, as `UpstreamSshServer` is (ADR-0456). With it
the `http/2` cases (358, 1700, 1702) and the h2c upgrade cases (1800, 1801) reach curl. On Windows
every one of them stops at once: Curl matches curl's Schannel build there, whose `curl -V` has no
`HTTP2` and which refuses `--http2` with exit 2 (`CurlVersionText`, ADR-0397). Yet
`UpstreamCurlPlatform` reported `http/2` and `h2c` on every platform, so on Windows these cases
were measured as failures that real curl's Windows build would never run, and test471 passed on
Windows only because it never sends `--http2`.

## Decision

`UpstreamCurlPlatform` reports `http/2` and `h2c` on Linux and macOS only, as Curl's `curl -V` does.
On Windows a case naming either feature is `SkippedOnThisPlatform` (ADR-0488): `Inconclusive`, listed
or not, and measured by CI's Linux and macOS jobs. `%H2CVER` is `h2c`, as `runtests.pl` sets it,
on every platform. Screening lets a case naming the `http/2` server run only when the runner is
given an `UpstreamHttp2Server`.

## Consequences

- The h2c cases cannot be measured on a Windows lane. Which of them pass on Linux and macOS is
  read from CI and listed by a follow-up task, not guessed here.
- test471 is `Inconclusive` on Windows; its connection-reuse rows still pass on Linux and macOS.
