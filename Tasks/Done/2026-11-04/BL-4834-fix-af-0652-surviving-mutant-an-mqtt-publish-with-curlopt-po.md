---
id: BL-4834
title: Fix AF-0652: Surviving mutant: an MQTT publish with CURLOPT_POSTFIELDSIZE 0 can send every post field instead of none
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4834 — Fix AF-0652: Surviving mutant: an MQTT publish with CURLOPT_POSTFIELDSIZE 0 can send every post field instead of none

## Goal

The defect the audit office reported as AF-0652 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0652 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0652-surviving-mutant-an-mqtt-publish-with-curlopt-post.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241`

Mutant 'size < 0' -> 'size <= 0' in 'return size < 0 || size >= fields.Length ? fields : fields[..(int)size];' survived. PostFieldSize defaults to -1. With size 0 the original sends an empty payload; the mutant sends the whole post-field bytes. That changes request bytes, and no Engine test notices.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241:< -Member MqttPostDataOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Narrowed `touches` from the whole `Curl.Engine.UnitLibrary` to `Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs`: the fix is a test only; the production line is right and stays unchanged. No other task in Doing on origin/work/dark-factory names an Engine file. The narrowing was not published separately because this lane does not push.
- Added `TransferSettingsOf_PostFieldSizeZeroWithFieldsOnAnMqttUrl_PublishesAnEmptyPayload` (fields "hello", PostFieldSize 0, expects an empty payload).
- Criterion 1: lanes may not run `Audit/Tools`, so the mutant was applied by hand instead (`size < 0` changed to `size <= 0` at ManagedEasyTransfer.Context.cs:241). The new test failed against it (killed) and passed once the line was reverted. The quality auditor's re-audit confirms the result with its own tool.
- Criterion 2: `dotnet build Curl.slnx` is clean and the fast tests have 0 failures. The change is test-only and platform-neutral. No production code changed, so no quality run.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. An MQTT handle with CURLOPT_POSTFIELDSIZE 0 is pinned to an empty payload by a unit test that kills AF-0652's mutant
