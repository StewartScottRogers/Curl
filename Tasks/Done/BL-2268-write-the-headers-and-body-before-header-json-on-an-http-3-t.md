---
id: BL-2268
title: Write the headers and body before %{header_json} on an HTTP/3 transfer so upstream case 2503 passes on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Http3Server.UnitLibrary, Curl.Conformance.Http3Server.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2268 — Write the headers and body before %{header_json} on an HTTP/3 transfer so upstream case 2503 passes on Linux and macOS

## Goal

Upstream case 2503 (`-w '%{header_json}\n' --http3-only` against the nghttpx-h3 stand-in on `%HTTP3PORT`) writes what curl 8.22.0 does on Linux and macOS, and is listed in `PassingUpstreamCases.txt`.

## Context

BL-2264 (2026-10-15) made the conformance harness report `headers-api`, so 2503 runs on Linux and macOS (on Windows it is `SkippedOnThisPlatform` for `http/3`). Measured on Windows emulating the Linux build (temporary `UpstreamCurlPlatform.Unix` with `parsesAsWindowsBuild: false` in `UpstreamConformanceTests`, as BL-2261 did; reverted): `<verify><stdout>` differs at byte 0, expected `HTTP/3 200 \r\n` and got `{\n` - the header block and the `-foo-` body that runtests.pl's `--include` should put before the JSON are missing, though the same command over HTTP/2 (test2403) passes. Start by running 2503 under that emulation with `CURL_UPSTREAM_KEEP_LOGS=1` and reading `trace2503` to see whether the response arrives at all. Correct `touches` once the cause is found.

## Acceptance criteria

- [x] The cause is fixed with a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] 2503 passes under the Linux emulation and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] The fast tests are green.

## Notes

- Cause of the measurement in Context: BL-2264's emulation left out the `--ssl-no-revoke` BL-2261 used. Without it every
  `--cacert` QUIC case fails on Windows with `schannel: the revocation status is unknown` (exit 60, `trace2503`), so no
  response arrived and only `-w`'s `{}` was written; 2500, a listed case, fails the same way under that emulation.
- With the full emulation (Unix platform, `parsesAsWindowsBuild: false`, `--ssl-no-revoke` prepended in `RunCurlAsync`;
  reverted) 2503 got the header block, `-foo-` and the JSON, and differed only by a `server: nghttpx` line the nghttpx-h3
  stand-in added (read through the h2c stand-in's reply reader). Upstream's HTTP/3 cases 2501 and 2503 expect no `server`
  field (2500 strips it), so Curl itself was right; nothing in Curl.Console or Curl.Http3 changed.
- Fix (ADR-0502, decided by Claude under Stewart's delegation): `Http3ServerRequestRelay` drops `server` from the relayed reply
  and its 502. Tests: `Http3ServerRequestRelayTests.RelayAsync_BackendReply_HasNoServerFieldAsNghttpxH3Writes`,
  `RelayAsync_BackendRefuses_Answers502WithNoServerField`; `Http3ServerSessionTests` updated.
- `touches` corrected to the stand-in library, its tests and the passing list; no other task was in Doing on
  `origin/work/dark-factory`.
- Measured: 2503 and 2500 pass under the emulation; 2501 still skips for Debug. 2503 listed. Fast tests green (exit 0).
  `Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary`: 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. The HTTP/3 stand-in sends no server field, so upstream case 2503 passes on Linux and macOS
