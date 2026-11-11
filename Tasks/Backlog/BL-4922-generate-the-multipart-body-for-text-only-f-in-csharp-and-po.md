---
id: BL-4922
title: Generate the multipart body for text-only -F in --csharp and --powershell output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs, Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs, Curl.Extension.CodeGeneration.UnitTests/CodeGenerationTextFormTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4922 — Generate the multipart body for text-only -F in --csharp and --powershell output

## Goal

Generated code for `-F n=v` sends the form part (ADR-1138 excludes only `-F` with a file part and `-T`).

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-013 (Medium, confirmed: reproduced at least twice).

- Repro: `curl -sS --powershell f.ps1 --csharp f.cs -F n=v http://127.0.0.1:18800/echo -o NUL`, then run `f.ps1` against an `/echo` route returning the body.
- Expected: The server receives `Content-Disposition: form-data; name="n"` and `v`.
- Actual: `f.ps1` has the multipart content type but no `-Body`; `f.cs` has `new ByteArrayContent([])`; the server receives `Content-Length: 0`; no warning.
- Where to start: `GeneratedRequest.cs` and `CodeGenerationTransferHook.cs`.

## Acceptance criteria

- [ ] Running `f.ps1` and `f.cs` makes `/echo` show `Content-Disposition: form-data; name="n"` and `v`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
