---
id: AF-0987
title: Surviving mutant: EngineTransferSettings.PostWithoutData can default to true, turning every CLI CoAP GET into a POST and failing MQTT with exit 43
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/EngineTransferSettings.cs:EngineTransferSettings-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineTransferSettings.cs:57:false
task: BL-5789
tasks: BL-5789
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0987 - Surviving mutant: EngineTransferSettings.PostWithoutData can default to true, turning every CLI CoAP GET into a POST and failing MQTT with exit 43

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/EngineTransferSettings.cs:57`: Surviving mutant: EngineTransferSettings.PostWithoutData can default to true, turning every CLI CoAP GET into a POST and failing MQTT with exit 43.

## Evidence

Location: `Curl.Engine.UnitLibrary/EngineTransferSettings.cs:57`

Seeded mutation run: 'bool PostWithoutData = false);' -> 'bool PostWithoutData = true);' survived. The default matters. Curl.Console/EasyOptionsFromCommandLine.TransferSettingsOf builds EngineTransferSettings without naming PostWithoutData, so every command-line transfer takes the default. TransferContextFactory copies it into the context: CoapRequest picks POST when context.PostWithoutData is set, and the MQTT handler refuses a POST without data with exit 43. No Curl.Engine test checks the default.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineTransferSettings.cs:57:false -Member EngineTransferSettings -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-25 | 2026-11-25_1306.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: line 57, 'bool PostWithoutData = false' -> 'true', outcome survived. Runner's targeted mutation rerun on the clean audited commit: survived.

## Log

- 2026-11-22: filed proposed.
