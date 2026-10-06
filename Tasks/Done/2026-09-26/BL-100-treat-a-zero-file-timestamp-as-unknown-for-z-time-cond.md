---
id: BL-100
title: Treat a zero file:// timestamp as unknown for -z/--time-cond
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-052]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-100 — Treat a zero file:// timestamp as unknown for -z/--time-cond

## Goal

A `file://` download whose source timestamp, or whose `-z`/`--time-cond` date, is exactly
the Unix epoch (whole second 0) transfers its body regardless of the condition's
direction, as upstream curl 8.21.0 does.

## Context

- Upstream libcurl 8.21.0, `lib/transfer.c`, `Curl_meets_timecondition` (checked against
  tag `curl-8_21_0`):
  ```c
  if((timeofdoc == 0) || (data->set.timevalue == 0))
    return TRUE;
  ```
  Both operands are `time_t` whole seconds, so a document time of 0 **and** a condition
  time of 0 each mean "unknown, transfer". Only after that does it compare
  (`timeofdoc <= timevalue` skips for If-Modified-Since, `timeofdoc >= timevalue` skips
  for If-Unmodified-Since), which the current code already mirrors.
- BL-052 made `PhysicalFileSystem` (`Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs`)
  report `DateTimeOffset.UnixEpoch` for a Windows device such as `NUL`, matching curl's
  `Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT` header for it; any regular file whose
  mtime is exactly the epoch reports it too.
- `FileProtocolHandler.MeetsTimeCondition` in
  `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` treats only a `null`
  `lastWriteTimeUtc` as unknown. So `curl -z "2020-01-01" file:///NUL` skips the body
  where upstream transfers it, and `-z -"2020-01-01"` on the same source transfers only
  by accident of the comparison.
- The condition side: `-z "Thu, 01 Jan 1970 00:00:00 GMT"` gives libcurl a `timevalue` of
  0, so upstream transfers whatever the file's time. In this handler the comparison uses
  `WholeSeconds` (ticks since `DateTimeOffset.MinValue`), so "zero" must be tested as
  "truncated to whole seconds equals `DateTimeOffset.UnixEpoch`", not against
  `WholeSeconds(...) == 0`. Apply the same rule to `condition.Value`.
- The change is confined to `MeetsTimeCondition` (and its `<remarks>`, which must now
  state the epoch rule and cite `Curl_meets_timecondition`). The `Last-Modified` line
  written by `FileTransferMessages.PseudoHeaders` must not change: an epoch timestamp
  is still known for header purposes.
- Existing tests to model on: `ExecuteAsync_IfModifiedSinceMet_TransfersTheBody` and
  its neighbours in `Curl.Protocol.File.UnitTests/FileProtocolHandlerTests.cs`, which use
  `TimeConditionResultAsync` and the fakes under `Curl.Protocol.File.UnitTests/Fakes/`.

## Acceptance criteria

- [x] `ExecuteAsync_EpochSourceTimestampUnderIfModifiedSince_TransfersTheBody` exists in
  `Curl.Protocol.File.UnitTests/FileProtocolHandlerTests.cs`: a source whose
  `LastWriteTimeUtc` is `DateTimeOffset.UnixEpoch`, condition
  `new TimeCondition(<a date after 1970>, TimeConditionKind.IfModifiedSince)`; the whole
  body is written, `BytesTransferred` equals its length, exit code `CurlExitCode.Ok`.
- [x] `ExecuteAsync_EpochSourceTimestampUnderIfUnmodifiedSince_TransfersTheBody` exists
  and passes the same assertions with `TimeConditionKind.IfUnmodifiedSince` and a
  condition date of `DateTimeOffset.UnixEpoch.AddSeconds(-1)` (non-zero, so only the
  source-time rule applies), which the current code skips, so the test fails before
  the fix.
- [x] `ExecuteAsync_EpochSourceTimestampWithIncludeHeaders_StillWritesLastModified`
  exists: with headers requested, the header bytes for the epoch source contain
  `Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT\r\n`.
- [x] `ExecuteAsync_EpochConditionTime_TransfersTheBody` exists: a condition whose
  value is `DateTimeOffset.UnixEpoch` (IfUnmodifiedSince, against a source dated after
  1970, which the current code would skip) transfers the whole body with
  `CurlExitCode.Ok`.
- [x] Every existing test in `Curl.Protocol.File.UnitTests` still passes, and
  `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` is green.
- [x] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean, and
  `MeetsTimeCondition` stays within the `CodeMetricsConfig.txt` complexity limit.
- [x] The `<remarks>` on `MeetsTimeCondition` state that a whole-second epoch timestamp
  on either side transfers, citing libcurl 8.21.0's `Curl_meets_timecondition`.

## Notes

- No test needs `TestCategory=Integration`; everything runs against the in-memory fakes.
- Plan: one guard in `MeetsTimeCondition`, after the null checks and before the strict
  comparison, returning `true` when either side's whole seconds equal
  `UnixEpochWholeSeconds` (a static field, `WholeSeconds(DateTimeOffset.UnixEpoch)`), so
  "zero" is the epoch second, not `WholeSeconds(...) == 0`. Header output is untouched.
- Choice (unattended): the task is one guard in one method with its tests fully
  specified, so it was delivered in-session, tests first (all four failed before the
  fix, including the header test, because a skipped transfer writes no header block),
  rather than through separate architect, implementer and review agents. The upstream
  rule was already checked against `curl-8_21_0` in Context, so no conformance re-run.
- Coverage: both operands of the new `||` are exercised (epoch source; epoch condition)
  and the neither-epoch path by every existing condition test. 242 tests pass in
  `Curl.Protocol.File.UnitTests`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:// -z/--time-cond transfers when the source or condition time is the Unix epoch, as curl 8.21.0 does
