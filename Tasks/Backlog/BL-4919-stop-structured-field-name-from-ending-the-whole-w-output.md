---
id: BL-4919
title: Stop %{structured_field.<name>} from ending the whole -w output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/StructuredFieldWriteOutTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4919 — Stop %{structured_field.<name>} from ending the whole -w output

## Goal

`-w "%{structured_field.priority}"` prints the parsed field and the rest of the format.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-010 (Medium, confirmed: reproduced at least twice).

- Repro: Route `/sf` returning `Priority: u=1, i` (plus `Cache-Status`, `Content-Digest`). `curl -sS --structured-fields http://127.0.0.1:18800/sf -o NUL -w "A[%{structured_field.priority}]B\n"`
- Expected: Help text and ADR-1152: `A[Dictionary of 2: u=Integer 1; i=Boolean true]B`.
- Actual: Stdout is exactly `A[`; the rest is dropped; exit 0. `%{structured_fields}` works.
- Where to start: `TransferWriteOutVariables.cs` ~line 344 (ExtensionWriteOutValues lookup) and the `-w` parser's handling of a dotted name; values set in `Curl.Extension.StructuredFields.UnitLibrary/StructuredFieldsTransferHook.cs`.

## Acceptance criteria

- [ ] The repro prints `A[Dictionary of 2: u=Integer 1; i=Boolean true]B`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
