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
completed: 2026-09-27
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

- [x] Notes records the curl 8.21.0 command, stderr bytes and exit code for each of the five cases above.
- [x] Tests in `Curl.Protocol.File.UnitTests` pin, with a recording sink, that the handler reports "started" exactly once for a successful download, a successful upload, and each measured failure where curl printed the meter.
- [x] Tests in `Curl.Protocol.File.UnitTests` pin that the handler never reports "started" for a missing file (exit 37) nor for any other measured failure where curl printed no meter.
- [x] A test pins that the handler never calls the sink's byte-count members.
- [x] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`; new lines and branches are 100% covered.

## Notes

Measured 2026-09-27 with curl 8.21.0 (x86_64-w64-mingw32, Schannel) from Git Bash, stderr
redirected to a file, `ten.txt` holding `0123456789` with mtime 2026-01-01, in `Z:/tmp/m129`
(a path without spaces: a space in the path makes curl reject the URL with exit 3).
Stderr in `cat -A` form:

1. `curl -o out1 --max-filesize 5 file:///Z:/tmp/m129/ten.txt` - exit 63, meter shown:
   ```
     % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current^M$
                                    Dload  Upload  Total   Spent   Left   Speed^M$
   ^M  0      0   0      0   0      0      0      0                              0^M$
   curl: (63) Exceeded the maximum allowed file size (5) with 5 bytes^M$
   ```
2. `curl -o out2 -C 20 file:///Z:/tmp/m129/ten.txt` - exit 36, meter shown:
   ```
   ** Resuming transfer from byte position 20^M$
     % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current^M$
                                    Dload  Upload  Total   Spent   Left   Speed^M$
   ^M  0      0   0      0   0      0      0      0                              0^M$
   curl: (36) failed to resume file:// transfer^M$
   ```
3. `curl -o out3 -z "1 Jun 2026" file:///Z:/tmp/m129/ten.txt` - exit 0 (condition unmet, nothing written), meter shown:
   ```
     % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current^M$
                                    Dload  Upload  Total   Spent   Left   Speed^M$
   ^M  0      0   0      0   0      0      0      0                              0^M$
   ```
4. `curl -o out4 file:///Z:/tmp/m129/d/` (a directory) - exit 37, no meter:
   ```
   curl: (37) Could not open file Z:/tmp/m129/d/^M$
   ```
5. `curl -T ten.txt file:///Z:/tmp/m129/nodir/x.txt` - exit 23, meter shown:
   ```
     % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current^M$
                                    Dload  Upload  Total   Spent   Left   Speed^M$
   ^M  0      0   0      0   0      0      0      0                              0^M$
   curl: (23) cannot open Z:\tmp\m129\nodir\x.txt for writing^M$
   ```

For comparison: a missing file (`-o out5 file:///Z:/tmp/m129/missing.txt`) is exit 37 with
only `curl: (37) Could not open file Z:/tmp/m129/missing.txt^M$`; a plain download and a
plain `-T` upload both exit 0 with the meter.

Decision, following the measurements: a download reports "started" in `DownloadAsync`
straight after the source opens, so exit 37 (missing file, directory) reports nothing, and
exit 63, exit 36 and an unmet `-z` report it. Case 5 contradicts "open succeeded means
started" for uploads: curl's `lib/file.c` opens the upload destination in the do phase,
after connect, so the meter is up even when that open fails. The upload therefore reports
"started" at the top of `UploadAsync`, before `OpenForWriteAsync`. A URL the handler
rejects (exit 3) reports nothing in either direction. No byte counts are reported. No new
ADR: this is a placement inside the contract ADR-0045 already defines, recorded here and
in the options table of `Curl.Protocol.File.UnitLibrary/CLAUDE.md`.

Tests: `Curl.Protocol.File.UnitTests/FileProtocolHandlerProgressTests.cs` (10 tests) with
the new `Fakes/RecordingTransferProgress.cs`; the new handler lines are straight-line code
executed by those tests, so no new branch is left uncovered.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. file:// handler reports transfer started once the source opens (download) or before the destination opens (upload), never byte counts, matching curl 8.21.0's meter
