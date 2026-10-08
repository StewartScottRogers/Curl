---
id: BL-1686
title: Fix AF-0067: CLAUDE.md says Integration tests live only in *.IntegrationTests projects; 17 sit in five *.UnitTests projects
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-08
completed:
---
# BL-1686 — Fix AF-0067: CLAUDE.md says Integration tests live only in *.IntegrationTests projects; 17 sit in five *.UnitTests projects

## Goal

The defect the audit office reported as AF-0067 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0067 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0067-claude-md-says-integration-tests-live-only-in-inte.md`.

Location: `CLAUDE.md:30`

Location: `CLAUDE.md:30`

CLAUDE.md, 'Build and test commands': an Integration test 'lives only in a `Curl.<Area>.IntegrationTests` project, never in a `*.UnitTests` project', and 'A slow test that is pure computation is not an Integration test: it stays in its `*.UnitTests` project as a `LongRunning` test'. It also gives `dotnet test --filter "TestCategory=LongRunning"` with CURL_RUN_LONG_RUNNING_TESTS=1, 'without it they show as skipped'. In the code, 17 `[TestCategory("Integration")]` attributes sit in 11 files across Curl.Cli.UnitTests (1 file), Curl.Console.UnitTests (3), Curl.Core.UnitTests (1), Curl.Cryptography.UnitTests (4) and Curl.Protocol.Ssh.UnitTests (2). Among them are the pure-computation tests X25519Tests/X448Tests ...Rfc7748Section52MillionIterations... and Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest... No test anywhere carries TestCategory("LongRunning"), and nothing reads CURL_RUN_LONG_RUNNING_TESTS. Only one IntegrationTests project exists (Curl.Networking.IntegrationTests). Phase 2: explained by ADR-0421 (Accepted 2026-10-07), whose rollout tasks BL-1599, BL-1600, BL-1603 and BL-1604 are still in Backlog. The document states as fact a rule that is still being rolled out.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Get-ChildItem -Path *.UnitTests -Recurse -Filter *.cs | Select-String -SimpleMatch '[TestCategory("Integration")]' | Where-Object { $_.Line.Trim() -eq '[TestCategory("Integration")]' }).Count
```

- Expected: 0
- Actual: 17

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
