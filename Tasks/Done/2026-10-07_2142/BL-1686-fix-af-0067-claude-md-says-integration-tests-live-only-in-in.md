---
id: BL-1686
title: Fix AF-0067: CLAUDE.md says Integration tests live only in *.IntegrationTests projects; 17 sit in five *.UnitTests projects
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1604]
touches: [CLAUDE.md]
requirement: none
created: 2026-10-08
completed: 2026-10-07
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-07 (lane 1): CLAUDE.md states the rule ADR-0421 decided, and the rollout has since moved the Cli, Console, Core and Ssh tests into their own `*.IntegrationTests` projects (BL-1599, BL-1600 and siblings are Done). The reproduction now gives 4, not 17: the four Curl.Cryptography.UnitTests files (BrainpoolEcdsaTests, Cast128Tests, X25519Tests, X448Tests), which BL-1604 retags to `LongRunning` and which also brings in `CURL_RUN_LONG_RUNNING_TESTS`. BL-1604 is in Doing on another lane and touches Curl.Cryptography.UnitTests, outside this task. Decision: leave CLAUDE.md as it is - softening it to "being rolled out" would make it false again the moment BL-1604 lands. This task depends on BL-1604; once it is Done, rerun the reproduction (expect 0), confirm a `LongRunning` test and the variable exist, and complete.
- 2026-10-07 (lane 3): BL-1604 is Done. The reproduction now gives 0; Curl.Cryptography.UnitTests carries `TestCategory("LongRunning")` (Cast128Tests) and `RunsOnlyWhenLongRunningTestsAreEnabledAttribute` reads `CURL_RUN_LONG_RUNNING_TESTS`. CLAUDE.md is true of the code as it stands, so it is left unchanged. Build clean, fast tests green.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Waits on BL-1604: the reproduction's last 4 hits are the Curl.Cryptography.UnitTests tests BL-1604 retags to LongRunning
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Reproduction gives 0 now that BL-1604 retagged the last four; CLAUDE.md is true as written
