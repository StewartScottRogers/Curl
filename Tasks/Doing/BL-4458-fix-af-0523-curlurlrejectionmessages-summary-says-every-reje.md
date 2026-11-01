---
id: BL-4458
title: Fix AF-0523: CurlUrlRejectionMessages' summary says every rejection prints 'curl: (3) URL rejected: ' (exit 67 aside), but three rejections print bare text, two with exits 43 and 27
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-11-01
completed:
---
# BL-4458 — Fix AF-0523: CurlUrlRejectionMessages' summary says every rejection prints 'curl: (3) URL rejected: ' (exit 67 aside), but three rejections print bare text, two with exits 43 and 27

## Goal

The defect the audit office reported as AF-0523 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0523 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0523-curlurlrejectionmessages-summary-says-every-reject.md`.

Location: `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs:5`

Location: `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs:5`

The class summary (lines 3-7) reads: 'The text curl 8.21.0's curl_url_strerror gives each CurlUrlRejection, which curl prints as curl: (3) URL rejected: and the text (exit 67 for CurlUrlRejection.UserNotAllowed).' The code that prints these messages does otherwise. Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:41-42 IsBareText returns true for IdnConversionFailed, UrlTooLong and UrlAtLengthLimit, so UrlRejectionText (line 37-38) prints those three with no 'URL rejected: ' prefix. ExitCodeOf (lines 44-50) maps UrlTooLong to CurlExitCode.BadFunctionArgument (43) and UrlAtLengthLimit to CurlExitCode.OutOfMemory (27), not 3. The enum's own docs (CurlUrlRejection.cs:48-64) state this correctly, so the class summary contradicts both the enum and the engine. A reader taking the summary at its word would expect exit 3 and a 'URL rejected: ' line for an over-long URL. Messages[10] and [11] are 'A libcurl function was given a bad argument' and 'Out of memory', the CURLE_ texts and not curl_url_strerror texts, which the summary also misdescribes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs -Pattern 'UrlTooLong => CurlExitCode|UrlAtLengthLimit => CurlExitCode|IsBareText\(rejection\) \?'
```

- Expected: No match: every rejection but UserNotAllowed is printed as 'URL rejected: <text>' with exit 3, as the CurlUrlRejectionMessages summary states.
- Actual: TransferPerformer.Setup.cs:38: IsBareText(rejection) ? rejection.ToCurlMessage() : UrlRejectedPrefix + rejection.ToCurlMessage(); TransferPerformer.Setup.cs:47: CurlUrlRejection.UrlTooLong => CurlExitCode.BadFunctionArgument, TransferPerformer.Setup.cs:48: CurlUrlRejection.UrlAtLengthLimit => CurlExitCode.OutOfMemory,

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
