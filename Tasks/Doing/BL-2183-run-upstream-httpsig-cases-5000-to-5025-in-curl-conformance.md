---
id: BL-2183
title: Run upstream httpsig cases 5000 to 5025 in Curl.Conformance.UnitTests with their key files and created=0
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2182]
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary, Curl.Console]
requirement: none
created: 2026-10-13
completed:
---
# BL-2183 — Run upstream httpsig cases 5000 to 5025 in Curl.Conformance.UnitTests with their key files and created=0

## Goal

Upstream curl 8.22.0's httpsig cases 5000 to 5025 run in `Curl.Conformance.UnitTests` and the
ones Curl passes are in `PassingUpstreamCases.txt`.

## Context

BL-2182 sends the `--httpsig-*` options' `Signature-Input` and `Signature` headers through
`Curl.Console`'s `HttpSignatureHttpAuthenticator`, and refuses bad options as upstream's
`config2setopts.c` does. The cases still cannot run here: `UpstreamTestData` has no
`data/data-httpsig-ed25519.key` or `data/data-httpsig-hmac-sha256.key` (copy them from the
`curl-8_22_0` tarball's `tests/data`), and upstream's debug build fixes `created` to 0 through
`CURL_FORCETIME` and `CURL_HTTPSIG_CREATED`, which the harness must map onto the signing clock
`CurlComposition.CreateRunner(signingClock:)` takes. The ed25519 key in
`Curl.HttpMessageSignatures.UnitTests` already reproduces test 5001's signature (BL-2182); the
HMAC key there does not reproduce test 5002's, so use the tarball's file.

## Acceptance criteria

- [ ] Both key files are in `Curl.Conformance.UnitTests/UpstreamTestData/data`.
- [ ] The cases 5000 to 5025 Curl passes are listed in `PassingUpstreamCases.txt`; each one it
      fails is named under Notes with its first difference, or filed as a task.

## Notes

- 2026-10-13, lane 2, first pass: the task needs two more projects than it named. `%SRCDIR` has no
  value in `UpstreamCaseRunner` except for the two emulated libtest scripts, so cases naming
  `%SRCDIR/data/...` skip: `touches` gains `Curl.Conformance.UnitLibrary` (an optional
  `sourceDirectory` parameter on `RunAsync`, an optional `UpstreamCurlPlatform.WithFeatures`, and
  `httpsig` among the platform's common features). The dialing `CurlComposition.CreateRunner` the harness
  uses takes no `signingClock`, so `touches` gains `Curl.Console` (add `TimeProvider? signingClock = null`
  to that overload and `CreateDialingTransferDispatch`, passed on to `CreateProtocolHandlers`, and fix the two
  `cref`s that spell the overload's parameter list). `Curl.Console` is in BL-2170's `touches` (Doing), so
  this task goes back to Backlog until BL-2170 is Done.
- Work in place, uncommitted (the shift stashes it): both key files copied byte for byte from the
  `curl-8_22_0` tag into `UpstreamTestData/data` (csproj copy, `.gitattributes`, README and
  `Update-UpstreamTestData.ps1` updated); `UpstreamCurlPlatform.WithFeatures` and `httpsig` feature
  with tests; `RunAsync(..., sourceDirectory)` with tests; `UpstreamConformanceTests` passes
  `UpstreamTestDataFolder` as `sourceDirectory`, reports `Debug` only to a case that also names `httpsig`
  (`PlatformFor`), and passes `signingClock: new UnixEpochClock()` for commands with a `--httpsig*` option
  (that last line does not build until the `Curl.Console` overload takes `signingClock`).
- Still to do once `Curl.Console` is free: add the `signingClock` parameter, build, run the fast tests,
  list the passing 5001-5025 cases in `PassingUpstreamCases.txt` (name each failing one here with its
  first difference), add `--ai-help`-free note (no option changes), run `Measure-CodeQuality.ps1` once for
  `Curl.Conformance.UnitLibrary`, and document `sourceDirectory` in `Curl.Conformance.UnitLibrary/CLAUDE.md`.
- Case 5000 uses `<tool>` (`lib5000`), so it stays skipped; 5026 to 5028 are outside this task's range.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Backlog. Needs Curl.Console (signingClock on the dialing CreateRunner), which BL-2170 in Doing touches; retry once BL-2170 is Done
- 2026-10-13: Backlog -> Doing.
