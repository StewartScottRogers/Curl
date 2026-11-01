---
id: AF-0511
title: EasyTransferHost.Library's AltSvcUsesOverHttp: false can become true with no test failing, so Curl.Api and Libcurl handles would honour Alt-Svc received over plain http
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/EasyTransferHost.cs:PhysicalFileSystem-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EasyTransferHost.cs:41:false
task: none
tasks:
found: 2026-10-31
found-at: 23bacbc6c04b8e06a4eecb79636da4891dca4c01
scorecard: 2026-10-31_1149.md
duplicate-of:
closed: 2026-11-01
closed-how: mechanical
closed-by: 2026-11-01_0144.md
---
# AF-0511 - EasyTransferHost.Library's AltSvcUsesOverHttp: false can become true with no test failing, so Curl.Api and Libcurl handles would honour Alt-Svc received over plain http

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/EasyTransferHost.cs:41`: EasyTransferHost.Library's AltSvcUsesOverHttp: false can become true with no test failing, so Curl.Api and Libcurl handles would honour Alt-Svc received over plain http.

## Evidence

Location: `Curl.Engine.UnitLibrary/EasyTransferHost.cs:41`

Invoke-MutationTest.ps1 -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0 reported 'survived Curl.Engine.UnitLibrary/EasyTransferHost.cs:41 false' (member as the tool names it: PhysicalFileSystem): 'AltSvcUsesOverHttp: false);' mutated to 'AltSvcUsesOverHttp: true);' and every Curl.Engine.UnitTests test passed. This is the host every Curl.Api and Libcurl handle runs on (EasyTransferHost.Library). ManagedEasyTransfer.SetUp.cs:207 passes host.AltSvcUsesOverHttp to OpenAltSvcCacheAsync, and its parameter documentation reads 'Whether the alt-svc cache also serves plain http origins (CURL_ALTSVC_HTTP)'. Under the mutant, a library handle would use an Alt-Svc entry learnt over plain http and connect to the alternative host, which libcurl does only in its debug build with CURL_ALTSVC_HTTP. No Engine test pins the Library host's value. It changes where a transfer connects, which is a decision rather than output bytes, so Medium.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EasyTransferHost.cs:41:false -Member PhysicalFileSystem -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test pins that a library handle ignores Alt-Svc over plain http
- Actual: outcome survived

## Re-audits

- 2026-11-01 | 2026-11-01_0144.md | reproduces: no | Ran the -Site command: the mutant AltSvcUsesOverHttp: true at EasyTransferHost.cs:41 was killed; ManagedCurlEngineTests.CreateEasy_ProductionEngine_GivesAHandleWithDefaultOptions now asserts Assert.IsFalse(Host.AltSvcUsesOverHttp). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-31: filed proposed.
- 2026-11-01: proposed -> closed. Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
