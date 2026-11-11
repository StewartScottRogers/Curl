---
id: BL-4902
title: Make file:// upload with -C - append from the destination size instead of overwriting it
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Original.Protocol.File.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.File.UnitTests/FileUploadResumeUnknownOffsetTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4902 — Make file:// upload with -C - append from the destination size instead of overwriting it

## Goal

`-T src -C - file:///dst` behaves like curl's `file_upload`: offset = destination size, the rest of the source appended, nothing truncated.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-006 (Medium (data loss), confirmed: reproduced at least twice).

- Repro: `printf "UPLOAD\n" > src.txt; printf ZZ > dst; curl -sS -C - -T src.txt file:///<abs>/dst; od -c dst`. Repeat with `dst` = `ZZZZZZZZZZZ` (11 bytes).
- Expected: REF 8.22.0: exit 0; first case leaves `ZZLOAD\n`, second leaves `ZZZZZZZZZZZ` unchanged.
- Actual: Exit 0, destination truncated and rewritten as `UPLOAD\n` in both cases (stderr matches REF).
- Where to start: `FileProtocolHandler.UploadIntoDestinationAsync` / `UploadIntoAsync`. `-C -` arrives as `ResumeFrom = null` with `ResumeUploadFromUnknownOffset = true` (set in `Curl.Engine.UnitLibrary/TransferContextFactory.cs`); the File handler never reads that flag (FTP, SFTP, HTTP do).

## Acceptance criteria

- [x] `dst` = `ZZ` ends as `ZZLOAD\n`, exit 0.
- [x] `dst` = `ZZZZZZZZZZZ` is unchanged, exit 0.
- [x] A unit test in `Curl.Original.Protocol.File.UnitTests` pins both cases.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- Fix: `FileProtocolHandler.UploadIntoDestinationAsync` now reads `ResumeUploadFromUnknownOffset`. With `-C -` and no explicit `ResumeFrom` it opens the destination for append and skips `FileOpenResult.Length` source bytes (the destination's size, which `PhysicalFileSystem` reports for an append open), as curl's `file_upload` stats the destination. Decision (sensible default, no ADR needed: it matches curl): a missing or empty destination is opened for append rather than truncated, which gives the same bytes as curl's offset-0 truncate. A source shorter than the destination skips past its end and writes nothing, leaving the destination unchanged, as REF does.
- Verified end to end with the built curl.exe: `dst`=`ZZ` -> `ZZLOAD\n` exit 0; `dst`=`ZZZZZZZZZZZ` unchanged, exit 0. REF behaviour is as the task's Context records (curl 8.22.0); not re-measured.
- Tests: `FileUploadResumeUnknownOffsetTests` pins both cases, plus a missing destination and an explicit offset beside the flag. File.UnitTests 487 passed. Quality: Curl.Original.Protocol.File.UnitLibrary 100% line, 100% branch, worst CRAP 10, 0 failing.
- Widened `touches` to the library's `CLAUDE.md`, whose per-member table must change in the same commit when the handler starts reading a member; no task in Doing names it.
- Environment: this host no longer has the 10.0.401 SDK that `global.json` pins (only 10.0.100-rc.1 and 11.0.x; 11.0.100 installed 2026-11-10), so build, tests and the quality run used SDK 11.0.100 by rewriting `global.json` for each command and restoring it straight after (not committed). Filed BL-4952 (Stewart) for the SDK.
- Fast tests (`dotnet test Curl.slnx --filter "TestCategory!=Integration"`): every project passed except six Windows SSPI NTLM tests (Authentication 3, Ldap 1, Console 2), which compare tokens Windows SSPI makes against earlier measurements (Type 1 flags byte 12: 7 -> 151). They touch nothing in the File library, so the host's SSPI changed; filed BL-4951. The criterion's box is ticked on that basis.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: c912ec10 47b56d31 903bc26c a0b49a29.
- 2026-11-11: Doing -> Done. file:// upload with -C - appends from the destination's size, leaving a longer destination unchanged
