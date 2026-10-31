---
id: BL-4433
title: Fix AF-0511: EasyTransferHost.Library's AltSvcUsesOverHttp: false can become true with no test failing, so Curl.Api and Libcurl handles would honour Alt-Svc received over plain http
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-31
completed:
---
# BL-4433 — Fix AF-0511: EasyTransferHost.Library's AltSvcUsesOverHttp: false can become true with no test failing, so Curl.Api and Libcurl handles would honour Alt-Svc received over plain http

## Goal

The defect the audit office reported as AF-0511 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0511 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0511-easytransferhost-library-s-altsvcusesoverhttp-fals.md`.

Location: `Curl.Engine.UnitLibrary/EasyTransferHost.cs:41`

Location: `Curl.Engine.UnitLibrary/EasyTransferHost.cs:41`

Invoke-MutationTest.ps1 -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0 reported 'survived Curl.Engine.UnitLibrary/EasyTransferHost.cs:41 false' (member as the tool names it: PhysicalFileSystem): 'AltSvcUsesOverHttp: false);' mutated to 'AltSvcUsesOverHttp: true);' and every Curl.Engine.UnitTests test passed. This is the host every Curl.Api and Libcurl handle runs on (EasyTransferHost.Library). ManagedEasyTransfer.SetUp.cs:207 passes host.AltSvcUsesOverHttp to OpenAltSvcCacheAsync, and its parameter documentation reads 'Whether the alt-svc cache also serves plain http origins (CURL_ALTSVC_HTTP)'. Under the mutant, a library handle would use an Alt-Svc entry learnt over plain http and connect to the alternative host, which libcurl does only in its debug build with CURL_ALTSVC_HTTP. No Engine test pins the Library host's value. It changes where a transfer connects, which is a decision rather than output bytes, so Medium.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EasyTransferHost.cs:41:false -Member PhysicalFileSystem -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test pins that a library handle ignores Alt-Svc over plain http
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
