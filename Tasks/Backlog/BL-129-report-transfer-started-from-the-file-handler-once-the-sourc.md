---
id: BL-129
title: Report transfer started from the file:// handler once the source is open
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-134]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-129 — Report transfer started from the file:// handler once the source is open

## Goal

The `file://` handler reports "transfer started" through the BL-134 progress sink at the point curl 8.21.0's meter begins, and reports no byte counts, so `Curl.Console` (after BL-130) shows the meter after a `file://` failure exactly when curl does.

## Context

curl 8.21.0 prints the progress meter for a transfer that got past connect/open and then failed, and not for one that failed before: a `file://` of a missing file prints only `curl: (37) Could not open file ...` (measured 2026-09-26, curl 8.21.0 x86_64-w64-mingw32). BL-130 makes `Curl.Console` print the meter when the handler signalled "started", even on failure. This task makes `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` send that signal.

It must not report byte counts: curl's `file://` status line stays `\r  0      0   0      0   0      0      0      0                              0` even after ten bytes (BL-102's Notes), so reporting bytes would make BL-131's live status line diverge.

**Measure first.** Before writing code, run curl 8.21.0 with `-o` to a file and stderr redirected to a file (not a terminal), and record under Notes the exact stderr bytes (`cat -A` form) and exit code of each case, to learn whether curl shows the meter:

- `--max-filesize 5` on a ten-byte file (exit 63 expected),
- `-C 20` on a ten-byte file (resume past the end),
- `-z` with a date after the file's timestamp (a successful bodiless transfer, for comparison),
- a directory URL (exit 37 expected),
- a `-T` upload to a `file://` destination in a directory that does not exist.

Report "started" at exactly the point the measurements put the start of curl's meter. If a case contradicts "open succeeded means started", record it under Notes and follow the measurement. Tests use the existing fakes in `Curl.Protocol.File.UnitTests/Fakes/`; add a recording sink there.

## Acceptance criteria

- [ ] Notes records the curl 8.21.0 command, stderr bytes and exit code for each of the five cases above.
- [ ] Tests in `Curl.Protocol.File.UnitTests` pin, with a recording sink, that the handler reports "started" exactly once for a successful download, a successful upload, and each measured failure where curl printed the meter.
- [ ] Tests in `Curl.Protocol.File.UnitTests` pin that the handler never reports "started" for a missing file (exit 37) nor for any other measured failure where curl printed no meter.
- [ ] A test pins that the handler never calls the sink's byte-count members.
- [ ] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`; new lines and branches are 100% covered.

## Notes

## Log

- 2026-09-26: Created.
