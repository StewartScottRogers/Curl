---
id: BL-1388
title: Write curl's 'The requested document is not new enough' -v line for an unmet file:// -z, and ignore -z under -r or -C as file_do does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: FR-009
created: 2026-10-03
completed:
---
# BL-1388 — Write curl's 'The requested document is not new enough' -v line for an unmet file:// -z, and ignore -z under -r or -C as file_do does

## Goal

A `file://` download whose file fails `-z` writes curl 8.21.0's `* The requested document is not new enough` (or `not old enough` for `-z -date`) before `* shutting down connection #0`, and a `file://` download with `-r` or a positive `-C` ignores `-z` entirely, as curl's `file_do` does.

## Context

- Today `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` `DownloadFromAsync` (around line 330) returns `TransferResult.TimeConditionNotMet()` when `MeetsTimeCondition` fails, without reporting a line, and checks the condition whatever `context.Range`, `context.RangeText` or `context.ResumeFrom` say.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/file.c` lines 422-424: `if(fstated && !data->state.range && data->set.timecondition && !Curl_meets_timecondition(data, data->info.filetime)) return CURLE_OK;` - the condition is only checked when no range is set. `lib/transfer.c` lines 121-145 (`Curl_meets_timecondition`) write `infof(data, "The requested document is not new enough")` for if-modified-since and `"The requested document is not old enough"` for if-unmodified-since, and treat a time of 0 on either side as met (already modelled by `MeetsKnownTimeCondition`).
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) against a 3-byte file `hi\n` last modified 2001-01-01:
  - `-sv -z "Jan 1 2020" file:///.../f.txt`: stderr exactly `* The requested document is not new enough\n* shutting down connection #0\n`; nothing on stdout; exit 0. With `-i` added: the same two lines and no header block.
  - `-sv -z "-Jan 1 1999" ...`: `* The requested document is not old enough\n* shutting down connection #0\n`; exit 0.
  - `-sv -r 0-0 -z "Jan 1 2020" ...`: `{ [1 bytes data]`, `* shutting down connection #0`; stdout `h`; exit 0 (condition ignored).
  - `-sv -C 1 -z "Jan 1 2020" ...`: `{ [2 bytes data]`, `* shutting down connection #0`; stdout `i\n`; exit 0 (condition ignored).
  - `-s -w '%{response_code}' -z "Jan 1 2020" ...` writes `000` (unchanged by this task).

## Acceptance criteria

- [ ] A test in `Curl.Protocol.File.UnitTests` over a fake file system with a file dated 2001-01-01 and an if-modified-since condition of 2020-01-01 pins the info line `The requested document is not new enough`, the transfer ending with nothing written and exit 0; a second test pins `The requested document is not old enough` for an if-unmodified-since condition of 1999-01-01.
- [ ] Tests pin that with `Range` 0-0 and with `ResumeFrom = 1` the same unmet condition is ignored and the bytes `h` and `i\n` are written.
- [ ] Tests pin that a met condition, and a file or condition time at the Unix epoch, write no new line.
- [ ] Test paths are drive-less (`file:///dir/f.txt` style through the fake file system), so the tests pass on Windows, Linux and macOS.
- [ ] `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- `-r` text that names no range (`5-2`) also sets curl's `state.range`, so `-z` is skipped there too and the exit 33 after the open (BL-1334) follows whatever the condition says; a test pins that with an unmet condition.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
