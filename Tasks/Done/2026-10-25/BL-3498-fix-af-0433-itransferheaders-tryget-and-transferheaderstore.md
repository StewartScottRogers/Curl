---
id: BL-3498
title: Fix AF-0433: ITransferHeaders.TryGet (and TransferHeaderStore.TryGet) returns a CurlHeaderCode, not a bool as a Try method should
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Api.UnitLibrary, Curl.Engine.UnitLibrary/TransferHeaderStore.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Libcurl/Headers/CurlEasyHeaders.cs, Curl.Engine.UnitTests, Libcurl.UnitTests/Headers, Curl.Conformance.LibTest.UnitTests/FakeTransferHeaders.cs, Documentation/Planning/Decisions/ADR-0512-websocket-header-and-connect-only-contracts.md]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3498 — Fix AF-0433: ITransferHeaders.TryGet (and TransferHeaderStore.TryGet) returns a CurlHeaderCode, not a bool as a Try method should

## Goal

The defect the audit office reported as AF-0433 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0433 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0433-itransferheaders-tryget-and-transferheaderstore-tr.md`.

Location: `Curl.Api.UnitLibrary/EngineContracts/Headers/ITransferHeaders.cs:21`

Location: `Curl.Api.UnitLibrary/EngineContracts/Headers/ITransferHeaders.cs:21`

ITransferHeaders.cs:21 declares 'CurlHeaderCode TryGet(string name, int index, CurlHeaderOrigin origin, int request, out CurlHeader? header);', and Curl.Engine.UnitLibrary/TransferHeaderStore.cs:107 implements it, returning CurlHeaderCode.Missing, BadIndex, the argument-check code or Ok. The naming rule in .claude/agents/align-and-document.md ('What aligned means') says a method named Try... returns bool. This one returns libcurl's CURLHcode result, so 'if (headers.TryGet(...))' does not compile, and a reader expects true/false where the method hands back curl_easy_header's code. A name such as Get or Find mirroring curl_easy_header would say what it does.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitLibrary/EngineContracts/Headers/ITransferHeaders.cs, Curl.Engine.UnitLibrary/TransferHeaderStore.cs -Pattern 'CurlHeaderCode TryGet\(' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: No match: a Try-named method returns bool, or the method returning CurlHeaderCode is not named Try...
- Actual: ITransferHeaders.cs:21 and TransferHeaderStore.cs:107

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Renamed `TryGet` to `Find` (mirrors curl_easy_header, returns CurlHeaderCode) in ITransferHeaders, TransferHeaderStore and every caller and fake: Curl.Engine.UnitLibrary, Libcurl, and the Engine, Libcurl and Conformance.LibTest tests (no other Doing task touches them), plus Engine CLAUDE.md and ADR-0512 text. A pure rename adds no branches, so Measure-CodeQuality was not run. Linux and macOS CI: change is platform-neutral.

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Done. Renamed TryGet to Find across API, engine, Libcurl and tests; build and fast tests green
