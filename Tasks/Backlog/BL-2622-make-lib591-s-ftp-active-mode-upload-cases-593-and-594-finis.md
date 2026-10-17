---
id: BL-2622
title: Make lib591's FTP active-mode upload cases 593 and 594 finish instead of timing out
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2622 — Make lib591's FTP active-mode upload cases 593 and 594 finish instead of timing out

## Goal

Upstream cases 593 and 594 (lib591: an active-mode FTP upload through a multi handle whose server answers the data connection with an error) finish within the case's accept timeout and pass through the runner, as 591 and 592 already do.

## Context

Found by BL-2533: `Lib591` passes 591 and 592 (on the ratchet), but 593 and 594 run about 20 seconds and fail. Read their `<servercmd>` and `<verify>` in `Curl.Conformance.UnitTests/UpstreamTestData`, run each with `CURL_UPSTREAM_CASE=<n>`, and in the first pass find whether the FTP engine's accept wait or the runner's FTP stand-in is at fault; add that project to `touches` before any edit.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
