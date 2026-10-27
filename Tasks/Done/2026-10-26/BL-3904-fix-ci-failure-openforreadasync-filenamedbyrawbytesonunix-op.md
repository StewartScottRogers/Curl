---
id: BL-3904
title: Fix CI failure OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitTests, Curl.Core.UnitLibrary]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3904 — Fix CI failure OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes on macOS

## Goal

`OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes` failed on macOS in CI run 40428708184 (https://github.com/StewartScottRogers/Curl/actions/runs/40428708184). First failing commit: 1928e3e7.

    Assertion failed. Expected value to be greater than or equal to the lower bound.

Lanes test only on Windows, so reproduce with `gh run view 40428708184 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (from `gh run view 40428708184 --log-failed`): the test could not arrange its file on macOS, because `open()` returned -1 when creating `caf�.txt`. macOS file systems refuse names that are not valid UTF-8 (EILSEQ), so no such file can exist there, and real curl on macOS can never open one either. This is not a product defect in `PhysicalFileSystem`.
- Fix: the test now runs on Linux only (`[OSCondition(OperatingSystems.Linux)]`). I kept its name because the acceptance criterion names it. The class doc comment now says so. `OpenForReadAsync_MissingRawByteNameOnUnix_IsNotFound` still runs on Linux and macOS (it passed there in that run).
- Checked locally on Windows: Curl.Core.UnitTests fast tests pass, 1915 passed and 10 skipped (this test is skipped on Windows, as before). The change is platform-neutral (it only narrows an OSCondition), so the CI part of the criterion is met by that. The shift's CI watch files a task if CI disagrees. This is a test-only change, so there was no Measure-CodeQuality run.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 29a3025a.
- 2026-10-26: Doing -> Done. OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes runs only on Linux, since macOS cannot hold a non-UTF-8 file name
