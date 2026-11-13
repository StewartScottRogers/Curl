---
id: BL-5067
title: Remove the orphaned remoteHeaderNameWasBlank doc comment from CurlCommandRunner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5067 — Remove the orphaned remoteHeaderNameWasBlank doc comment from CurlCommandRunner

## Goal

`IpfsQueryMessage` in `CurlCommandRunner` carries only its own doc comment, and the stale comment for the removed `remoteHeaderNameWasBlank` field is gone.

## Context

- Found by the shift-end review of 2026-11-12 (review-20261112-200028), comment and code review of e032d6195..acc2af773.
- `Curl.Console/CurlCommandRunner.cs:620-623`: BL-5064 moved the flag to `RunningTransferState.RemoteHeaderNameWasBlank` and deleted the field, but left its `/// <summary>` block behind.
- The C# compiler joins consecutive `///` blocks, so `IpfsQueryMessage` (line 629) now has two `<summary>` elements. The first one says it is "Set when a Content-Disposition name was blank". That is false, and CLAUDE.md treats a misleading doc as a defect.

## Acceptance criteria

- [ ] `Curl.Console/CurlCommandRunner.cs` has no `<summary>` that mentions a blank `Content-Disposition` name above `IpfsQueryMessage`.
- [ ] `grep -n "remoteHeaderNameWasBlank" Curl.Console/*.cs` finds nothing.
- [ ] `dotnet build` is clean and the fast tests for `Curl.Console.UnitTests` are green.

## Notes

## Log

- 2026-11-12: Created.
