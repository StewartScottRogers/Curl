---
id: BL-4716
title: Fix AF-0568: CI red 34.63 minutes from run 41560119555 (bcc7d1f5): MQTT 5 conformance test Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 failed on Linux and macOS
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-03
completed:
---
# BL-4716 — Fix AF-0568: CI red 34.63 minutes from run 41560119555 (bcc7d1f5): MQTT 5 conformance test Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 failed on Linux and macOS

## Goal

The cause of the factory behaviour the audit office reported as AF-0568 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0568 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0568-ci-red-34-63-minutes-from-run-41560119555-bcc7d1f5.md`.

Location: `logs/DarkFactory-20261102-174304.log:58`

Location: `logs/DarkFactory-20261102-174304.log:58`

Spell 2026-11-03T01:43:57Z to 02:18:35Z, 34.63 minutes, from run 41560119555 on bcc7d1f5 (logs/ci-runs.json:709). BL-4596 was filed at 18:46:45 Arizona, claimed by L2 at 18:49:52 (opus, 20 turns, 8.6 minutes) and integrated at 19:02:41: 'The MQTT5 TLS refusal case counts a refusal after a completed handshake too, so it passes on Linux and macOS'. Run 41561329126 also failed. The coordinator cancelled 41562433776 at 19:05:35 because it lacked the fix. Run 41563931008 went green at 02:18:35Z. Lanes test only on Windows, so a platform-dependent conformance case reached CI unseen.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41560119555; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-174304*.log -Pattern 'BL-4596'
```

- Expected: No CI red spell over 30 minutes.
- Actual: Spell 2026-11-03T01:43:57Z to 02:18:35Z, 34.63 minutes, runId 41560119555; BL-4596 filed 18:46:45, DONE 19:02:41.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
