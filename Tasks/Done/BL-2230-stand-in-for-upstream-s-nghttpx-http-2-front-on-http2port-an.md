---
id: BL-2230
title: Stand in for upstream's nghttpx HTTP/2 front on %HTTP2PORT and %HTTP2TLSPORT so cases 358, 359, 1700, 1702, 1800, 1801 and 2400-2406 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2244]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.Http2Server.UnitLibrary, Curl.Conformance.Http2Server.UnitTests, Curl.slnx]
model: opus
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2230 — Stand in for upstream's nghttpx HTTP/2 front on %HTTP2PORT and %HTTP2TLSPORT so cases 358, 359, 1700, 1702, 1800, 1801 and 2400-2406 run

## Goal

Stand in for upstream's `http/2` server (nghttpx in front of `sws`) on `%HTTP2PORT` (cleartext, h2c) and `%HTTP2TLSPORT` (TLS, ALPN h2), and give `%H2CVER` a value, so cases 358, 359, 1700, 1702, 1800, 1801 and 2400-2406 run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; running `Curl.Conformance.UnitTests` on 2026-10-14 names 1700, 1702, 358, 359 ("the harness does not emulate the http/2 server"), 2400-2404 and 2406 (`%HTTP2TLSPORT`) and 1800, 1801 (`%H2CVER`). The gap tool uses `Curl.Conformance.UnitLibrary`. Stewart's standing rule is that nothing is left out because it is hard: the HTTP/2 server side is hand-built.

`%HTTP2PORT` is already 9009 (`UpstreamCaseRunner.Http2Port`) but nothing stands behind it. nghttpx terminates HTTP/2 and speaks HTTP/1.1 to `sws` (`servers.pm` at curl-8_22_0): the stand-in is an HTTP/2 server connection (preface, SETTINGS, HEADERS/DATA, HPACK, flow control, GOAWAY, h2c upgrade and prior knowledge, TLS with ALPN `h2`) that turns each stream into an HTTP/1.1 request relayed to an `sws` emulation connection and its reply back into HTTP/2 frames, as nghttpx does, including the headers nghttpx adds (`Via`, `X-Forwarded-*`) that the cases' `<protocol>` expects. Curl already has an HTTP/2 client in `Curl.Protocol.Http2.UnitLibrary` (check the name in `Curl.slnx`); this library may not reference protocol libraries, so the frame and HPACK server code goes in a new `Curl.Conformance.Http2Server.UnitLibrary` (+ `.UnitTests`) like the SSH stand-in (ADR-0456), registered in `Curl.slnx`, held to the same quality gates; add the stand-in's entry to `UpstreamCurlInvocation`/the runner through a record the caller passes, as `UpstreamSshServer` is. `%H2CVER` is what `runtests.pl` gives (read it). This is the largest stand-in: deliver h2c (cases 358, 1700, 1702, 1800, 1801, 2400-2402) first and the TLS port (359, 2403, 2404, 2406) second, filing the second as a follow-up task if one lane run cannot hold both. The task's `touches` already name the two new projects and `Curl.slnx`.

## Acceptance criteria

- [x] (BL-2244) The new library exists, listed in `Curl.slnx`, with `.UnitTests`, 100% line and branch coverage, complexity at most 10, CRAP at most 30, AOT-compatible; it references no `Curl.Protocol.*` library other than what the SSH stand-in's precedent allows, and `dotnet build -warnaserror` is clean.
- [x] (h2c and its tests: BL-2244; TLS: BL-2245) A connection to `%HTTP2PORT` speaks h2c (upgrade and prior knowledge) and relays to `sws`; one to `%HTTP2TLSPORT` speaks h2 over TLS (`test-localhost.pem`, ALPN `h2`); tests pin the preface, a GET, a POST with body, a server push refusal, GOAWAY on close, and an `Upgrade: h2c` request.
- [x] (`%HTTP2TLSPORT`: BL-2245) `%H2CVER` and `%HTTP2TLSPORT` have values; screening lets `http/2` cases run.
- [x] (measured off Windows by BL-2259; 2400-2402 use `%HTTP2TLSPORT`, so BL-2245) Cases 358, 1700, 1702, 1800, 1801, 2400-2402 (and 359, 2403, 2404, 2406 if the TLS port is in this task) are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [x] Platform-neutral tests; `Measure-CodeQuality.ps1` shows no member of `Curl.Conformance.UnitLibrary` or the new library outside a gate; `dotnet test --filter "TestCategory!=Integration"` is green; both libraries' `CLAUDE.md` files are updated.

## Notes

- 2026-10-14 (lane 1, first pass before any code): split. The whole stand-in - an HTTP/2 server connection, the relay to sws, the runner wiring, the TLS front and triage of 13 cases, each under the quality gates - does not fit one run's cost cap. Split into:
  - BL-2244: the h2c server library itself (`Curl.Conformance.Http2Server.UnitLibrary` + `.UnitTests`, `Curl.slnx`). It reuses the HPACK and frame codec already in `Curl.Http2.UnitLibrary`, which references nothing, so it is not a `Curl.Protocol.*` dependency. This task now depends on it.
  - BL-2245: the TLS front on `%HTTP2TLSPORT` (cases 359, 2403, 2404, 2406). It depends on this task.
  - What is left here: put BL-2244's server on `%HTTP2PORT` through a record the caller passes (as `UpstreamSshServer` is), give `%H2CVER` the value `runtests.pl` sets at curl-8_22_0 (read it there; `h2c` is expected), let screening run `http/2` cases on the h2c port, and triage 358, 1700, 1702, 1800, 1801 and 2400-2402. The TLS criteria above belong to BL-2245.

- 2026-10-14 (lane 1, second claim): done.
  - `UpstreamHttp2Server` (`Curl.Conformance.UnitLibrary`) is the record the caller passes as `UpstreamCaseRunner`'s optional `http2Server`, as `UpstreamSshServer` is: `InFrontOf(otherPorts, backend)` puts a fresh h2c server from BL-2244's `UpstreamHttp2ServerFactory.Create` on 9009, relaying to the case's sws emulation, just inside the https-proxy stand-in. The conformance tests pass `new UpstreamHttp2Server(UpstreamHttp2ServerFactory.Create)`. Screening lets a case naming `http/2` run only when one is given (`FindSkipReason(..., emulatesHttp2)`).
  - `%H2CVER` is `h2c` (`UpstreamCaseRunner.H2cVersion`), the value `runtests.pl` sets; curl sends `Upgrade: h2c`, which 1800's expected protocol shows.
  - Measured: with the stand-in in place every h2c case reached curl on Windows and stopped at `curl: option --http2: the installed libcurl version does not support this` (exit 2), because Curl matches curl's Schannel build on Windows, which has no `HTTP2` (`CurlVersionText`). So the harness reported a feature Curl's Windows `curl -V` lacks. ADR-0495 (Decided by Claude under Stewart's delegation): `UpstreamCurlPlatform` reports `http/2` and `h2c` on Linux and macOS only; on Windows 358, 471, 1700, 1702, 1800 and 1801 are `SkippedOnThisPlatform` (Inconclusive), and `InProcessCurl_ConnectionReuseCase_Passes` is Inconclusive for such a case (471).
  - The cases can therefore only be judged on Linux and macOS, which lanes do not run, so none is added to `PassingUpstreamCases.txt` here (listing an unmeasured case could turn CI red). BL-2259 reads the CI ratchet rows, lists the passing ones and files a task per failing cause.
  - 2400-2402 use `%HTTP2TLSPORT`, so they belong with 359, 2403, 2404 and 2406 to BL-2245.
  - Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, 0 failing members. `Curl.Conformance.Http2Server.UnitLibrary`'s code is unchanged (only its `CLAUDE.md`). Fast tests green; `Curl.Conformance.UnitTests` 3068 passed, 0 failed.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Backlog. Split: waits on BL-2244 (the h2c server library); the TLS port is BL-2245
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. The h2c stand-in stands on %HTTP2PORT through UpstreamHttp2Server, %H2CVER is h2c, and http/2 cases run off Windows (ADR-0495); BL-2259 lists them from CI
