---
id: AF-0067
title: CLAUDE.md says Integration tests live only in *.IntegrationTests projects; 17 sit in five *.UnitTests projects
auditor: truthfulness
severity: Medium
status: accepted
reason: 
key: truthfulness:CLAUDE.md:IntegrationTestPlacement:false-statement
reproduction: none
task: BL-1686
tasks: BL-1686
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0067 - CLAUDE.md says Integration tests live only in *.IntegrationTests projects; 17 sit in five *.UnitTests projects

## Summary

Medium finding from the truthfulness auditor at `CLAUDE.md:30`: CLAUDE.md says Integration tests live only in *.IntegrationTests projects; 17 sit in five *.UnitTests projects. Reported by an auditor flagged unreliable in 2026-10-07_1336.md.

## Evidence

Location: `CLAUDE.md:30`

CLAUDE.md, 'Build and test commands': an Integration test 'lives only in a `Curl.<Area>.IntegrationTests` project, never in a `*.UnitTests` project', and 'A slow test that is pure computation is not an Integration test: it stays in its `*.UnitTests` project as a `LongRunning` test'. It also gives `dotnet test --filter "TestCategory=LongRunning"` with CURL_RUN_LONG_RUNNING_TESTS=1, 'without it they show as skipped'. In the code, 17 `[TestCategory("Integration")]` attributes sit in 11 files across Curl.Cli.UnitTests (1 file), Curl.Console.UnitTests (3), Curl.Core.UnitTests (1), Curl.Cryptography.UnitTests (4) and Curl.Protocol.Ssh.UnitTests (2). Among them are the pure-computation tests X25519Tests/X448Tests ...Rfc7748Section52MillionIterations... and Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest... No test anywhere carries TestCategory("LongRunning"), and nothing reads CURL_RUN_LONG_RUNNING_TESTS. Only one IntegrationTests project exists (Curl.Networking.IntegrationTests). Phase 2: explained by ADR-0421 (Accepted 2026-10-07), whose rollout tasks BL-1599, BL-1600, BL-1603 and BL-1604 are still in Backlog. The document states as fact a rule that is still being rolled out.

## Reproduction

Run from the repository root:

```powershell
(Get-ChildItem -Path *.UnitTests -Recurse -Filter *.cs | Select-String -SimpleMatch '[TestCategory("Integration")]' | Where-Object { $_.Line.Trim() -eq '[TestCategory("Integration")]' }).Count
```

- Expected: 0
- Actual: 17

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
