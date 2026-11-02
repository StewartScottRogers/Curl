---
id: AF-0523
title: CurlUrlRejectionMessages' summary says every rejection prints 'curl: (3) URL rejected: ' (exit 67 aside), but three rejections print bare text, two with exits 43 and 27
auditor: truthfulness
severity: Low
status: accepted
reason: Stewart accepted it on 2026-11-01 ("accept all").
key: truthfulness:Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs:CurlUrlRejectionMessages:false-doc-comment
reproduction: none
task: BL-4458
tasks: BL-4458
found: 2026-11-01
found-at: 4c39311e3e4524ac6950cb2cba36f5b3f2a6ff2e
scorecard: 2026-11-01_1016.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0523 - CurlUrlRejectionMessages' summary says every rejection prints 'curl: (3) URL rejected: ' (exit 67 aside), but three rejections print bare text, two with exits 43 and 27

## Summary

Low finding from the truthfulness auditor at `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs:5`: CurlUrlRejectionMessages' summary says every rejection prints 'curl: (3) URL rejected: ' (exit 67 aside), but three rejections print bare text, two with exits 43 and 27. Reported by an auditor flagged unreliable in 2026-11-01_1016.md.

## Evidence

Location: `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs:5`

The class summary (lines 3-7) reads: 'The text curl 8.21.0's curl_url_strerror gives each CurlUrlRejection, which curl prints as curl: (3) URL rejected: and the text (exit 67 for CurlUrlRejection.UserNotAllowed).' The code that prints these messages does otherwise. Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:41-42 IsBareText returns true for IdnConversionFailed, UrlTooLong and UrlAtLengthLimit, so UrlRejectionText (line 37-38) prints those three with no 'URL rejected: ' prefix. ExitCodeOf (lines 44-50) maps UrlTooLong to CurlExitCode.BadFunctionArgument (43) and UrlAtLengthLimit to CurlExitCode.OutOfMemory (27), not 3. The enum's own docs (CurlUrlRejection.cs:48-64) state this correctly, so the class summary contradicts both the enum and the engine. A reader taking the summary at its word would expect exit 3 and a 'URL rejected: ' line for an over-long URL. Messages[10] and [11] are 'A libcurl function was given a bad argument' and 'Out of memory', the CURLE_ texts and not curl_url_strerror texts, which the summary also misdescribes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs -Pattern 'UrlTooLong => CurlExitCode|UrlAtLengthLimit => CurlExitCode|IsBareText\(rejection\) \?'
```

- Expected: No match: every rejection but UserNotAllowed is printed as 'URL rejected: <text>' with exit 3, as the CurlUrlRejectionMessages summary states.
- Actual: TransferPerformer.Setup.cs:38: IsBareText(rejection) ? rejection.ToCurlMessage() : UrlRejectedPrefix + rejection.ToCurlMessage(); TransferPerformer.Setup.cs:47: CurlUrlRejection.UrlTooLong => CurlExitCode.BadFunctionArgument, TransferPerformer.Setup.cs:48: CurlUrlRejection.UrlAtLengthLimit => CurlExitCode.OutOfMemory,

## Re-audits

- 2026-11-01 | 2026-11-01_1801.md | reproduces: no | Ran the reproduction: Select-String matched TransferPerformer.Setup.cs:38 (IsBareText(rejection) ? rejection.ToCurlMessage() : UrlRejectedPrefix + ...), :47 (UrlTooLong => CurlExitCode.BadFunctionArgument, 43) and :48 (UrlAtLengthLimit => CurlExitCode.OutOfMemory, 27). The code is unchanged. The statement under audit, CurlUrlRejectionMessages' summary (Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs:3-11), now reads 'URL rejected: and the text with exit 3 for most rejections, exit 67 for UserNotAllowed, and the bare text with no prefix for those three [IdnConversionFailed, UrlTooLong, UrlAtLengthLimit], with exit 3, 43 and 27 respectively', which matches what the reproduction shows. It no longer says every rejection prints the prefix.

## Log

- 2026-11-01: filed proposed.
- 2026-11-01: proposed -> accepted. Stewart: accept all.
