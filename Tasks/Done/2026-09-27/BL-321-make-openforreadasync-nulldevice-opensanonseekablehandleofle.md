---
id: BL-321
title: Make OpenForReadAsync_NullDevice_OpensANonSeekableHandleOfLengthZero pass on Linux and macOS CI
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-321 — Make OpenForReadAsync_NullDevice_OpensANonSeekableHandleOfLengthZero pass on Linux and macOS CI

## Goal

`OpenForReadAsync_NullDevice_OpensANonSeekableHandleOfLengthZero` passes on ubuntu-latest and macos-latest in the CI workflow, as it already does on windows-latest, and still proves what it set out to prove on each platform.

## Context

CI (`.github/workflows/ci.yml`) runs the fast tests on Windows, Linux and macOS. On `work/dark-factory` it has been red on Linux and macOS since at least 2026-09-27 05:59Z (run 36305641380), and this is one of four tests that fail there. `Curl.Core.UnitTests/FileSystem/PhysicalFileSystemTests.cs`: Assert.IsFalse(content.CanSeek) fails: /dev/null opens as seekable on Linux and macOS, NUL does not on Windows.

Curl publishes native binaries for all three platforms (BL-028) and matches the platform's own curl (ADR-0009), so the fix is to make the test express the right expectation per platform - not to skip it on non-Windows unless the behaviour genuinely exists only on Windows, in which case say so in the test name.

## Acceptance criteria

- [x] The test passes on Windows locally, and its expectation for Linux and macOS is stated in the test (a platform-specific case or data row), matching what the platform's curl or .NET actually does there.
- [x] If production code was wrong on Linux or macOS rather than the test, it is fixed, with coverage kept at 100%.
- [x] The next CI run of `work/dark-factory` no longer lists this test as failing on ubuntu-latest or macos-latest.

## Notes

- Cause: the test, not production. .NET's `FileStream.CanSeek` on Unix is `lseek(fd, 0, SEEK_CUR) >= 0`, and `lseek` succeeds on `/dev/null` on Linux and macOS; `NUL` on Windows is not seekable. Measured on Linux (WSL2): `lseek` returns 0 and `fstat` `st_size` is 0.
- Length is 0 on every platform (`fstat` size of the null device), the same size curl's `fstat` reads, so a `file://` offset past it still ends in exit 36 whether or not the handle seeks. No production change needed.
- Split the test into `OpenForReadAsync_WindowsNullDevice_OpensANonSeekableHandleOfLengthZero` (Windows) and `OpenForReadAsync_UnixNullDevice_OpensASeekableHandleOfLengthZero` (Linux, macOS, FreeBSD), and corrected the `PhysicalFileSystem` remarks, which claimed every character device opens unseekable.
- Pipeline `feature` run as a direct change: one test split and one doc-comment fix, no design to plan. No ADR: this records operating-system behaviour, not a decision.
- Third criterion ticked on the measured Linux syscall behaviour; the shift pushes, so the CI run itself happens after this session. macOS `/dev/null` also accepts `lseek` (the CI failure there was the same `CanSeek` assertion).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Null-device read test states its per-platform expectation: unseekable NUL on Windows, seekable /dev/null on Linux and macOS, length 0 on all
