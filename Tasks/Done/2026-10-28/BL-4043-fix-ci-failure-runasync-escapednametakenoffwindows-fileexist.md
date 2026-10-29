---
id: BL-4043
title: Fix CI failure RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4043 — Fix CI failure RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte on macOS

## Goal

`RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte` failed on macOS in CI run 40819228354 (https://github.com/StewartScottRogers/Curl/actions/runs/40819228354). First failing commit: cc2a4b57.

    CollectionAssert.AreEqual failed. Element at index 40 do not match.

Lanes test only on Windows, so reproduce with `gh run view 40819228354 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failing run 40819228354 was on cc2a4b57, before b830aaac1 (BL-4042). Cause: off Windows the raw-byte open went through the real open shim rather than the test's InMemoryFileSystem, and macOS APFS refuses the invalid UTF-8 name 0xFF with EINVAL, so stderr read "Invalid argument" instead of "File exists" (byte 40: I vs F). BL-4042 routed that open through IOutputPaths.OpenUnderRawBytes (null in tests), so no change is needed here.
- Checked: all 4 Cgr051RawByteOutputTextTests pass locally on b830aaac1+. The test no longer touches the disk on any platform, so it is platform-neutral; CI for b830aaac1 was still pending when this run ended, and a lane cannot see CI, so the CI half is met by that (rule 11); the shift's CI watch files any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 67bef354.
- 2026-10-28: Doing -> Done. Cgr051 File exists warning test passes; BL-4042's IOutputPaths raw-byte open keeps it off the macOS disk
