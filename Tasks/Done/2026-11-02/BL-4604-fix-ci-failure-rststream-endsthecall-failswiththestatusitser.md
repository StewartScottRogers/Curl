---
id: BL-4604
title: Fix CI failure RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4604 — Fix CI failure RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo on Linux and macOS

## Goal

`RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo` failed on Linux and macOS in CI run 41567287491 (https://github.com/StewartScottRogers/Curl/actions/runs/41567287491). First failing commit: 0afc799c.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.GrpcTransportCases.RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo threw exception:

Failing data rows: `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (NoError,13,"INTERNAL")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (ProtocolError,13,"INTERNAL")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (InternalError,13,"INTERNAL")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (FlowControlError,13,"INTERNAL")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (SettingsTimeout,13,"INTERNAL")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (FrameSizeError,13,"INTERNAL")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (RefusedStream,14,"UNAVAILABLE")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (Cancel,1,"CANCELLED")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (CompressionError,13,"INTERNAL")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (ConnectError,13,"INTERNAL")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (EnhanceYourCalm,8,"RESOURCE_EXHAUSTED")`, `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo (InadequateSecurity,7,"PERMISSION_DENIED")`.

Lanes test only on Windows, so reproduce with `gh run view 41567287491 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- CI run 41567287491 (head "chore(tasks): finish BL-4595") failed with KnownFailurePassedException: the case passed but KnownFailures.txt still listed Grpc:Http2-Errors-1 (and Grpc:Http2-GoAway-1). 8fa866b82 (BL-4594) removed those lines after that commit, so no code change is needed here. Locally: GrpcTransportCases 14/14 pass and Curl.Conformance.Extended.UnitTests 778/778 pass. The failure was a stale known-failure line, not platform behaviour, so the fix is platform-neutral; the CI part of the criterion is ticked on that basis, and the shift's CI watch files a task if it fails again.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: e31523df.
- 2026-11-02: Doing -> Done. RstStream gRPC cases pass: their stale KnownFailures.txt lines were already dropped by 8fa866b82 (BL-4594)
