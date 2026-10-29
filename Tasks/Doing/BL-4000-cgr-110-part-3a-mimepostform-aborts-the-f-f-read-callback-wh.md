---
id: BL-4000
title: CGR-110 part 3a: MimePostForm aborts the -F f=@- read callback when stdin is closed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570, BL-3893, BL-3972, BL-3973, BL-3985]
touches: [Curl.Engine.UnitLibrary/MimePostForm.cs, Curl.Console.UnitTests/Cgr110Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4000 — CGR-110 part 3a: MimePostForm aborts the -F f=@- read callback when stdin is closed

## Goal

`MimePostForm.WithStandardInput`'s read callback returns the read-abort result when its stream is a `ClosedStandardInputStream`, so a libcurl-style form part read from a closed stdin ends with exit 42.

## Context

Split from BL-3974 (CGR-110, Documentation/Reviews/Curl-Gap-Report-2026-10-25.md). `ClosedStandardInputStream` now lives in `Curl.Core` (BL-3985) and ADR-0967 records the decision (exit 42 for a `-F` part from a closed standard input), so `Curl.Engine` can see the type. The unit work is in a stash; see Notes.

## Acceptance criteria

- [ ] `MimePostForm.WithStandardInput`'s read callback (`StandardInputReadOf`) returns `ReadCallbackResult.Abort` when the stream is a `ClosedStandardInputStream`, and 0 for an ordinary empty stream; its doc comment cites ADR-0967.
- [ ] Unit tests in `Curl.Console.UnitTests/Cgr110Tests.cs` (abort when closed, 0 when empty) fail before and pass after (ADR-0482).
- [ ] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes.

## Notes

- Split from BL-3974 (cost cap: its runs cost too much for one run).
- Continues the stashed work of BL-3974.
- From the stash take only `Curl.Engine.UnitLibrary/MimePostForm.cs` and `Curl.Console.UnitTests/Cgr110Tests.cs`; the `ClosedStandardInputStream` move already landed with BL-3985 and ADR-0967 exists (the stash's doc comment cites ADR-0956 as a placeholder: change it to ADR-0967).
- Real curl measured (see BL-3974 Notes): exit 42, stderr `curl: (42) operation aborted by callback`, stdout empty.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: edbfc467 69ae9e49 c259902e.
