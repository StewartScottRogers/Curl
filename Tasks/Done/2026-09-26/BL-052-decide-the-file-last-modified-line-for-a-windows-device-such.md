---
id: BL-052
title: Decide the file:// Last-Modified line for a Windows device such as NUL
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-009]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-052 — Decide the `file://` `Last-Modified` line for a Windows device such as `NUL`

## Goal

`curl -sI file:///NUL` prints the same header bytes as curl 8.21.0 on Windows, or the
difference is recorded as deliberate.

## Context

ADR-0002's BL-018 amendment measured curl 8.21.0 on Windows printing
`Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT` for `file:///NUL`. BL-009's
`PhysicalFileSystem` reads the timestamp with `File.GetLastWriteTimeUtc(SafeFileHandle)`,
which throws for `NUL`, so it reports `null` and the handler leaves the whole
`Last-Modified` line out. Upstream's stat of a Windows device evidently succeeds with a
zero time. Options: report the Unix epoch for a device handle on Windows, or keep `null`
and record the divergence (the latter needs Stewart, as a deliberate divergence).

## Acceptance criteria

- [x] `PhysicalFileSystem` reports, for `NUL` on Windows, a `LastWriteTimeUtc` that makes
      `FileProtocolHandler` emit curl 8.21.0's measured header block, pinned by an
      `[TestCategory("Integration")]` test in `Curl.Core.UnitTests`; or a Stewart-approved
      divergence is recorded in ADR-0002.

## Notes

- Decision (sensible default, no divergence needed): match upstream. On Windows,
  `PhysicalFileSystem` reports `DateTimeOffset.UnixEpoch` for a handle whose last-write
  time `File.GetLastWriteTimeUtc(SafeFileHandle)` cannot read (a device such as `NUL`),
  because the Windows C runtime's `fstat` that curl 8.21.0 calls reports zero for it.
  Off Windows an unreadable timestamp stays `null`, as before. Chosen because it makes
  the bytes match the measurement and needs no approval; keeping `null` would have been a
  deliberate divergence.
- The platform choice is an injected constructor flag
  (`reportsUnreadableTimestampAsEpoch`), like `setsUnixCreateMode`, so both branches are
  covered by fast Windows tests.
- Tests: `OpenForReadAsync_WindowsNullDeviceReportingEpoch_ReportsTheUnixEpoch`,
  `OpenForReadAsync_WindowsNullDeviceNotReportingEpoch_ReportsNoTimestamp` (fast), and
  `OpenForReadAsync_WindowsNullDevice_ReportsCurlsMeasuredLastModifiedDate`
  (`Integration`), which pins the `R`-formatted date `FileProtocolHandler` writes.
- Not checked end to end: `Curl.Console` does not parse `-I` yet, so `curl -sI
  file:///NUL` exits 2 with "option -I: is unknown".
- Follow-up filed: BL-100. Upstream's `Curl_meets_timecondition` treats a document time
  of 0 as unknown and transfers; `FileProtocolHandler` only treats `null` so, so `-z` on
  `NUL` now skips a body upstream would send.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:///NUL on Windows reports the Unix epoch, so its Last-Modified line matches curl 8.21.0
