---
id: BL-4945
title: Print extension warnings on stderr as curl prints --libcurl warnings
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs, Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4945 — Print extension warnings on stderr as curl prints --libcurl warnings

## Goal

A failed `--csharp`/`--powershell` write and a failed `--otel-endpoint` export print a `Warning:` line on stderr (silenced by `-s`), and `--otel-endpoint notaurl` is refused or warned about.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-023 (Low, confirmed: reproduced at least twice).

- Repro: `curl --csharp Z:/nonexistent_dir_q/x.cs http://127.0.0.1:18800/plain`; `curl -v --otel-endpoint http://127.0.0.1:18899/x http://127.0.0.1:18800/plain` (nothing on 18899); `curl -s --otel-endpoint notaurl http://127.0.0.1:18800/plain`.
- Expected: ADR-1138: warns "as --libcurl only warns" (`Warning: Failed to open ... to write libcurl code`); ADR-1134: a failed export "is logged as a warning".
- Actual: Nothing on stderr, even with `-v`; visible only with `--log-level warning`; `notaurl` accepted silently.
- Where to start: `CodeGenerationTransferHook.cs:39` and `OpenTelemetryTransferHook.cs:39` write only to `DiagnosticLog`, which `CurlCommandRunner.OpenDiagnosticLogAsync` enables only for `--log-level`. Use a drive-less path in tests.

## Acceptance criteria

- [ ] Both failures print a `Warning:` line on stderr, and none with `-s`.
- [ ] `--otel-endpoint notaurl` is refused (exit 2) or warned about.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 9f0e1af2 eebf94bb a0b49a29.
