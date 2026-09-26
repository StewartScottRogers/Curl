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
completed:
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

- [ ] `PhysicalFileSystem` reports, for `NUL` on Windows, a `LastWriteTimeUtc` that makes
      `FileProtocolHandler` emit curl 8.21.0's measured header block, pinned by an
      `[TestCategory("Integration")]` test in `Curl.Core.UnitTests`; or a Stewart-approved
      divergence is recorded in ADR-0002.

## Notes

## Log

- 2026-09-26: Created.
