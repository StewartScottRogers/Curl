---
id: BL-5338
title: Write a Zone.Identifier stream for --xattr on Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/NativeExtendedAttributeWriter.cs, Curl.Console/OutputFileExtendedAttributes.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-5338 — Write a Zone.Identifier stream for --xattr on Windows

## Goal

On Windows, `--xattr` writes a `Zone.Identifier` alternate data stream holding `[ZoneTransfer]` and the origin URL without credentials, as curl 8.22 does. An ADR records the change from the earlier choice that Windows writes none.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-088 (Low, Confirmed twice; missing metadata). Area: `--xattr`, Windows.

**Repro.** `curl -sS --xattr "http://u:p@127.0.0.1:30080/hello?q=1#f" -o x.txt`, then in PowerShell `Get-Item x.txt -Stream *` and `Get-Content x.txt -Stream Zone.Identifier`.

**Expected (REF).** An alternate data stream `Zone.Identifier` (60 bytes): `[ZoneTransfer]\r\nHostUrl=http://127.0.0.1:30080/hello?q=1#f\r\n` (credentials removed). With `-L`, `HostUrl` is the original URL.

**Actual (PORT).** Only the `:$DATA` stream; exit 0, no message.

**Where to start.** `Curl.Console/NativeExtendedAttributeWriter.cs`: `ForCurrentPlatform()` returns `null` on Windows, and its remark that no Windows curl writes extended attributes is out of date for curl 8.22. This reverses that earlier choice (ADR-0320 documents the libc-only writer); record the reversal in a new ADR in `Documentation/Planning/Decisions/` marked "Decided by Claude under Stewart's delegation". `Curl.Console/OutputFileExtendedAttributes.cs` builds the attributes, `Curl.Console/IExtendedAttributeWriter.cs` is the seam, and `Curl.Console/CurlComposition.cs` (line about 122) wires the writer. On Windows the stream is written by opening `<path>:Zone.Identifier`.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/` (next free number) records that `--xattr` on Windows writes `Zone.Identifier` as curl 8.22 does, supersedes the "no Windows writer" remark, and is marked "Decided by Claude under Stewart's delegation".
- [ ] New tests in `Curl.Console.UnitTests` prove, through a stand-in `IExtendedAttributeWriter`, that on Windows the Zone.Identifier content for `http://u:p@host/hello?q=1#f` is exactly `[ZoneTransfer]\r\nHostUrl=http://host/hello?q=1#f\r\n` (credentials removed) and that with `-L` it names the original URL.
- [ ] Tests that call the real Windows stream write carry `[OSCondition(OperatingSystems.Windows)]` (and live in an Integration project if they touch more than a temp directory they create); the Linux and macOS behaviour (libc writer) is unchanged and its tests pass.
- [ ] The `--xattr` text in `--ai-help` (and `--help`/manual text, in `Curl.Cli.UnitLibrary` where it lives; add that file to `touches` when changed) describes the Windows behaviour correctly, and a test pins it.
- [ ] Hand check recorded in the Log: on Windows, `Get-Content x.txt -Stream Zone.Identifier` prints `[ZoneTransfer]` and `HostUrl=http://127.0.0.1:30080/hello?q=1#f`. Measure real curl 8.22 with `Record-CurlExchange.ps1` where it can; a server is a PowerShell or C# file-based app, never Python.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- BCL only; `NativeExtendedAttributeWriter` is excluded from fast coverage per ADR-0083, so keep any new Windows adapter thin and put the content logic in a covered type.
- Review acceptance check: `Get-Content x.txt -Stream Zone.Identifier` prints `[ZoneTransfer]` and `HostUrl=http://127.0.0.1:30080/hello?q=1#f`.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: ed9bf5ed a0057e61 ab56350a 90a1958e 51bfe1ee a0b49a29.
