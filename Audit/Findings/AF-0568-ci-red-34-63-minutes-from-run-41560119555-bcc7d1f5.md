---
id: AF-0568
title: CI red 34.63 minutes from run 41560119555 (bcc7d1f5): MQTT 5 conformance test Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 failed on Linux and macOS
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:ci-run-41560119555:ci-red
reproduction: none
task: BL-4716
tasks: BL-4716
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0568 - CI red 34.63 minutes from run 41560119555 (bcc7d1f5): MQTT 5 conformance test Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 failed on Linux and macOS

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261102-174304.log:58`: CI red 34.63 minutes from run 41560119555 (bcc7d1f5): MQTT 5 conformance test Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 failed on Linux and macOS.

## Evidence

Location: `logs/DarkFactory-20261102-174304.log:58`

Spell 2026-11-03T01:43:57Z to 02:18:35Z, 34.63 minutes, from run 41560119555 on bcc7d1f5 (logs/ci-runs.json:709). BL-4596 was filed at 18:46:45 Arizona, claimed by L2 at 18:49:52 (opus, 20 turns, 8.6 minutes) and integrated at 19:02:41: 'The MQTT5 TLS refusal case counts a refusal after a completed handshake too, so it passes on Linux and macOS'. Run 41561329126 also failed. The coordinator cancelled 41562433776 at 19:05:35 because it lacked the fix. Run 41563931008 went green at 02:18:35Z. Lanes test only on Windows, so a platform-dependent conformance case reached CI unseen.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41560119555; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-174304*.log -Pattern 'BL-4596'
```

- Expected: No CI red spell over 30 minutes.
- Actual: Spell 2026-11-03T01:43:57Z to 02:18:35Z, 34.63 minutes, runId 41560119555; BL-4596 filed 18:46:45, DONE 19:02:41.

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: yes | The given reproduction shows the 34.63-minute spell from 41560119555 and BL-4596 (Mqtt5s_UntrustedCertificate) on Linux and macOS. The same cause crossed the rule in this window: run 41655977963 (85f7b9d8) failed CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter on Linux only, a test that passed on lanes' Windows runs (lane 1 3132/3132 pass). The spell started 2026-11-03T15:01:02Z and lasted 95 minutes, still red at window end (BL-4732, lane 1, 08:02-08:05 MST).
- 2026-11-03 | 2026-11-03_1615.md | reproduces: yes | The reproduction still shows the historical spell (41560119555, 34.63 min, BL-4596 MQTT5 test on Linux and macOS). I re-audited by mechanism over this window: the same cause crossed the CI-red rule again. Run 41655977963 on 85f7b9d8 failed on Linux only, in CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter, which lanes testing only on Windows had passed. The spell lasted 2026-11-03T15:01:02Z to 17:52:45Z, 171.72 min. BL-4732 was filed at 08:01 Arizona and fixed on lane 1 at 08:04 (faccb10d4), but the next run, 41660275371, failed again at 08:25.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
