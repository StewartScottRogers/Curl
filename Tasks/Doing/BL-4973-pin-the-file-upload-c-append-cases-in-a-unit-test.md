---
id: BL-4973
title: Pin the file:// upload -C - append cases in a unit test
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-4972]
touches: [Curl.Original.Protocol.File.UnitTests/FileUploadResumeUnknownOffsetTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4973 — Pin the file:// upload -C - append cases in a unit test

## Goal

A unit test in `Curl.Original.Protocol.File.UnitTests` pins `-T src -C - file:///dst` appending from the destination size.

## Context

Split from BL-4902. BL-4972 makes the File handler honour `ResumeUploadFromUnknownOffset`; this task pins it, with the handler's lines and branches staying at 100% coverage.

## Acceptance criteria

- [x] `FileUploadResumeUnknownOffsetTests` has a test where `dst` = `ZZ` and the source is `UPLOAD\n`, asserting `ZZLOAD\n`.
- [x] It has a test where `dst` = `ZZZZZZZZZZZ` asserting the destination is unchanged.
- [x] Both tests are platform-neutral (no drive-letter path) and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Split from BL-4902 (over its cost cap).

The tests were already committed with BL-4972 (90a7a162c); verified they match the criteria and the File unit tests pass (487 passed). Use a temporary directory the test creates and deletes; no Python.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 42cc340a 947fbf8a c0432cfe.
